using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using PSRT.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    class Proxy
    {
        private ApplicationResources _ApplicationResources;
        private ILogger _BaseLogger;
        private ProxyListenerManager _ProxyListenerManager;

        private byte[] _RC4Key;

        private RC4Engine _IncomingRC4Encrypter;
        private RC4Engine _IncomingRC4Decrypter;

        private RC4Engine _OutgoingRC4Encrypter;
        private RC4Engine _OutgoingRC4Decrypter;

        private enum ConnectionDirection
        {
            Incoming,
            Outgoing
        }

        public Proxy(ILogger logger, ApplicationResources applicationResources, ProxyListenerManager proxyListenerManager)
        {
            _BaseLogger = new StagedLogger(new StagedLogger(logger, nameof(Proxy)), GetHashCode().ToString());
            _ApplicationResources = applicationResources;
            _ProxyListenerManager = proxyListenerManager;
        }

        public async Task RunAsync(TcpClient client, TcpClient server)
        {
            _BaseLogger.WriteLine("Proxy begin");

            var tasks = new[]
            {
                _HandleConnection(client, server, ConnectionDirection.Outgoing),
                _HandleConnection(server, client, ConnectionDirection.Incoming)
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _BaseLogger.WriteLine($"Proxy ended with unhandled exception -> {ex.Message}", LoggerLevel.Verbose);
            }

            _BaseLogger.WriteLine("Proxy end");
        }

        private async Task _HandleConnection(TcpClient input, TcpClient output, ConnectionDirection direction)
        {
            var logger = new StagedLogger(_BaseLogger, direction.ToString());

            logger.WriteLine("Handler begin");

            try
            {
                await _HandleConnectionInternal(input.GetStream(), output.GetStream(), direction, logger);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Exception in handler -> {ex.Message}", LoggerLevel.Verbose);
            }

            // ensure connections are teminated
            input.Client.Shutdown(SocketShutdown.Both);
            output.Client.Shutdown(SocketShutdown.Both);

            logger.WriteLine("Handler end");
        }

        private async Task _HandleConnectionInternal(Stream input, Stream output, ConnectionDirection direction, ILogger logger)
        {
            var acceptedBuffer = new List<byte>();
            var streamBuffer = new byte[8192];

            while (true)
            {
                try
                {
                    var count = await input.ReadAsync(streamBuffer, 0, streamBuffer.Length);
                    if (count == 0)
                    {
                        logger.WriteLine("End of input stream reached", LoggerLevel.VerboseTechnical);
                        break;
                    }

                    logger.WriteLine($"Data received -> Length: {count}", LoggerLevel.VerboseTechnical);

                    var decrypter = direction == ConnectionDirection.Incoming ? _IncomingRC4Decrypter : _OutgoingRC4Decrypter;
                    if (decrypter != null)
                    {
                        var decrypted = new byte[count];
                        decrypter.ProcessBytes(streamBuffer, 0, count, decrypted, 0);
                        acceptedBuffer.AddRange(decrypted);
                    }
                    else
                    {
                        acceptedBuffer.AddRange(streamBuffer.Take(count));
                    }

                    logger.WriteLine($"Current accepted data length -> {acceptedBuffer.Count}", LoggerLevel.VerboseTechnical);
                }
                catch (Exception ex)
                {
                    logger.WriteLine($"Error reading from stream -> {ex.Message}", LoggerLevel.VerboseTechnical);
                    break;
                }

                // enough data to construct packet length info
                while (acceptedBuffer.Count >= 4)
                {
                    var packetLengthBuffer = acceptedBuffer.Take(4).ToArray();
                    var packetLength = BitConverter.ToInt32(packetLengthBuffer, 0);

                    logger.WriteLine($"Packet length received -> {packetLength}", LoggerLevel.VerboseTechnical);

                    // not enough data to construct whole of packet, wait for more data
                    if (packetLength > acceptedBuffer.Count)
                        break;

                    // whole packet received, time to decode

                    var packetSignatureBuffer = acceptedBuffer.Skip(4).Take(2).ToArray();
                    var packetSignature = new PacketSignature
                    {
                        Type = packetSignatureBuffer[0],
                        Subtype = packetSignatureBuffer[1]
                    };

                    var packetFlagsBuffer = acceptedBuffer.Skip(6).Take(2).ToArray();
                    var packetFlags = BitConverter.ToUInt16(packetFlagsBuffer, 0);

                    logger.WriteLine($"Packet received -> Length: {packetLength}, Signature: {packetSignature}, Flags: 0x{packetFlags.ToString("x4")}", LoggerLevel.Verbose);

                    var packetBodyLength = packetLength - 8;
                    var packetBody = acceptedBuffer.Skip(8).Take(packetBodyLength).ToArray();

                    // remove packet data from buffer so it does not get processed again
                    acceptedBuffer.RemoveRange(0, packetLength);

                    // get encrypter early in case packet handling changes it
                    var encrypter = direction == ConnectionDirection.Incoming ? _IncomingRC4Encrypter : _OutgoingRC4Encrypter;

                    // default to unhandled packet
                    var unhandledPacket = new Packet(packetSignature, packetFlags, packetBody);
                    var handledPacket = await _HandlePacket(unhandledPacket, logger);

                    var responseBuffer = handledPacket.ToBytes();
                    // encrypt packet bytes if needed
                    if (encrypter != null)
                    {
                        var encrypted = new byte[responseBuffer.Length];
                        encrypter.ProcessBytes(responseBuffer, 0, responseBuffer.Length, encrypted, 0);
                        responseBuffer = encrypted;
                    }

                    try
                    {
                        await output.WriteAsync(responseBuffer, 0, responseBuffer.Length);
                    }
                    catch (Exception ex)
                    {
                        logger.WriteLine($"Error writing to stream -> {ex.Message}", LoggerLevel.VerboseTechnical);
                        break;
                    }
                }
            }
        }

        private async Task<Packet> _HandlePacket(Packet p, ILogger logger)
        {
            // TODO: proper packet resolver with fancy lambdas and stuff

            if (p.Signature.Equals(0x11, 0x2c))
            {
                var packet = new BlockInfoPacket(p);
                logger.WriteLine($"{nameof(BlockInfoPacket)} -> Address: {packet.Address}, Port: {packet.Port}", LoggerLevel.Verbose);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                return packet;
            }

            if (p.Signature.Equals(0x11, 0xb))
            {
                var packet = new KeyExchangePacket(p);
                logger.WriteLine($"{nameof(KeyExchangePacket)} -> Token: {packet.Token.ToHexString()}, RC4 key: {packet.RC4Key.ToHexString()}", LoggerLevel.Verbose);

                var keyParam = new KeyParameter(packet.RC4Key);

                _IncomingRC4Encrypter = new RC4Engine();
                _IncomingRC4Encrypter.Init(true, keyParam);

                _IncomingRC4Decrypter = new RC4Engine();
                _IncomingRC4Decrypter.Init(false, keyParam);

                _OutgoingRC4Encrypter = new RC4Engine();
                _OutgoingRC4Encrypter.Init(true, keyParam);

                _OutgoingRC4Decrypter = new RC4Engine();
                _OutgoingRC4Decrypter.Init(false, keyParam);

                _RC4Key = packet.RC4Key;

                logger.WriteLine($"Packets are now encrypted");
                return packet;
            }

            if (p.Signature.Equals(0x11, 0xc))
            {
                var packet = new TokenPacket(p, _RC4Key);
                logger.WriteLine($"{nameof(TokenPacket)} -> Token: {packet.Token.ToHexString()}", LoggerLevel.Verbose);
                return packet;
            }

            if (p.Signature.Equals(0x11, 0x0))
            {
                var packet = new LoginPacket(p);
                logger.WriteLine($"LoginPacket -> User: `{packet.User}`", LoggerLevel.Verbose);
                return packet;
            }

            if (p.Signature.Equals(0x11, 0x1))
            {
                var packet = new LoginConfirmationPacket(p);

                // general sellout
                var blockId = packet.BlockName.Substring(0, 5);
                var releaseString = 
                packet.BlockName = $"{blockId}:{Program.ApplicationName}";

                return packet;
            }

            if (p.Signature.Equals(0x11, 0x10))
            {
                var packet = new BlockListPacket(p);

                // translate block names
                for (int i = 0; i < packet.BlockInfos.Length; i++)
                {
                    var name = packet.BlockInfos[i].Name;
                    var title = name.Substring(6);
                    if (_ApplicationResources.BlockNameTranslations.ContainsKey(title))
                    {
                        var translation = _ApplicationResources.BlockNameTranslations[title];
                        packet.BlockInfos[i].Name = $"{name.Substring(0, 5)}:{translation}";
                    }
                }

                return packet;
            }

            if (p.Signature.Equals(0x11, 0x13))
            {
                var packet = new BlockReplyPacket(p);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                return packet;
            }

            // user rooms and team room both use the same packet structure
            if (p.Signature.Equals(0x11, 0x17) || p.Signature.Equals(0x11, 0x4f))
            {
                var packet = new RoomInfoPacket(p);
                logger.WriteLine($"{nameof(RoomInfoPacket)} -> Address: {packet.Address}, Port: {packet.Port}", LoggerLevel.Verbose);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                return packet;
            }

            if (p.Signature.Equals(0x11, 0x21))
            {
                var packet = new SharedShipPacket(p);
                logger.WriteLine($"{nameof(SharedShipPacket)} -> Address: {packet.Address}, Port: {packet.Port}", LoggerLevel.Verbose);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                return packet;
            }

            //if (p.Signature.Equals(0x31, 0x5))
            //{
            //    // TODO: can translate titles here

            //    var packet = new PackedStringPacket(p, PackedStringPacket.TitlePacketXor, PackedStringPacket.TitlePacketSub);

            //    //for (int i = 0; i < packet.Titles.Count; i++)
            //    //{
            //    //    packet.Titles[i] = new TitlePacket.TitleInfo
            //    //    {
            //    //        Id = packet.Titles[i].Id,
            //    //        Name = "Ayy lmao"
            //    //    };
            //    //}

            //    return packet;
            //}

            //if (p.Signature.Equals(0x4, 0x52))
            //{
            //    var packet = new DamagePacket(p);

            //    logger.WriteLine($"Damage received -> {packet.Value}");

            //    return packet;
            //}

            return p;
        }
    }
}
