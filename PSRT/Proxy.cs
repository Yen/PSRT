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
using System.Threading;
using System.Threading.Tasks;

namespace PSRT
{
    class ProxyPacketConsumer
    {
        private Stream _Output;
        private RC4Engine _Encrypter;
        private Proxy _Proxy;

        public ProxyPacketConsumer(Stream output, RC4Engine encrypter, Proxy proxy)
        {
            _Output = output;
            _Encrypter = encrypter;
            _Proxy = proxy;
        }

        public async Task Submit(Packet packet, ILogger logger)
        {
            try
            {
                var bytes = packet.ToBytes();

                if (_Encrypter != null)
                {
                    var encryptedBytes = new byte[bytes.Length];
                    _Encrypter.ProcessBytes(bytes, 0, bytes.Length, encryptedBytes, 0);
                    bytes = encryptedBytes;
                }

                await _Output.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Exception writing packet -> {ex.Message}", LoggerLevel.VerboseTechnical);
                _Proxy.Shutdown();
                throw;
            }
        }
    }

    class Proxy
    {
        private ApplicationResources _ApplicationResources;
        private ILogger _BaseLogger;
        private ProxyListenerManager _ProxyListenerManager;

        private TcpClient _Client;
        private TcpClient _Server;

        private byte[] _RC4Key;
        
        private RC4Engine _IncomingRC4Decrypter;
        private RC4Engine _OutgoingRC4Decrypter;

        private ProxyPacketConsumer _ClientConsumer;
        private ProxyPacketConsumer _ServerConsumer;

        private enum PacketSource
        {
            Client,
            Server
        }

        public Proxy(TcpClient client, TcpClient server, ILogger logger, ApplicationResources applicationResources, ProxyListenerManager proxyListenerManager)
        {
            _Client = client;
            _Server = server;

            _BaseLogger = new StagedLogger(new StagedLogger(logger, nameof(Proxy)), GetHashCode().ToString());
            _ApplicationResources = applicationResources;
            _ProxyListenerManager = proxyListenerManager;
        }

        public void Shutdown()
        {
            _BaseLogger.WriteLine("Shutting down", LoggerLevel.Verbose);

            try
            {
                _Client.Client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                _BaseLogger.WriteLine($"Error shutting down client socket-> {ex.Message}", LoggerLevel.Verbose);
            }

            try
            {
                _Server.Client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                _BaseLogger.WriteLine($"Error shutting down server socket -> {ex.Message}", LoggerLevel.Verbose);
            }
        }

        public async Task RunAsync()
        {
            _BaseLogger.WriteLine("Begin");

            _ClientConsumer = new ProxyPacketConsumer(_Client.GetStream(), null, this);
            _ServerConsumer = new ProxyPacketConsumer(_Server.GetStream(), null, this);

            var tasks = new[]
            {
                _HandleConnection(_Client, _Server, PacketSource.Client),
                _HandleConnection(_Server, _Client, PacketSource.Server)
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _BaseLogger.WriteLine($"Proxy ended with unhandled exception -> {ex.Message}", LoggerLevel.Verbose);
            }

            _BaseLogger.WriteLine("End");
        }

        private async Task _HandleConnection(TcpClient input, TcpClient output, PacketSource source)
        {
            var logger = new StagedLogger(_BaseLogger, source.ToString());

            try
            {
                await _HandleConnectionInternal(input.GetStream(), output.GetStream(), source, logger);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Exception in handler -> {ex.Message}", LoggerLevel.Verbose);
            }

            Shutdown();
        }

        private async Task _HandleConnectionInternal(Stream input, Stream output, PacketSource source, ILogger logger)
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

                    var decrypter = source == PacketSource.Server ? _IncomingRC4Decrypter : _OutgoingRC4Decrypter;
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

                    // default to unhandled packet
                    var unhandledPacket = new Packet(packetSignature, packetFlags, packetBody);
                    
                    var consumer = source == PacketSource.Server ? _ClientConsumer : _ServerConsumer;
                    await _HandlePacket(unhandledPacket, consumer, logger);
                }
            }
        }

        private async Task _HandlePacket(Packet p, ProxyPacketConsumer consumer, ILogger logger)
        {
            // TODO: proper packet resolver with fancy lambdas and stuff

            if (p.Signature.Equals(0x11, 0x2c))
            {
                var packet = new BlockInfoPacket(p);
                logger.WriteLine($"{nameof(BlockInfoPacket)} -> Address: {packet.Address}, Port: {packet.Port}", LoggerLevel.Verbose);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                await consumer.Submit(packet, logger);
                return;
            }

            if (p.Signature.Equals(0x11, 0xb))
            {
                var packet = new KeyExchangePacket(p);
                logger.WriteLine($"{nameof(KeyExchangePacket)} -> Token: {packet.Token.ToHexString()}, RC4 key: {packet.RC4Key.ToHexString()}", LoggerLevel.Verbose);

                _RC4Key = packet.RC4Key;

                var keyParam = new KeyParameter(_RC4Key);

                _IncomingRC4Decrypter = new RC4Engine();
                _IncomingRC4Decrypter.Init(false, keyParam);

                _OutgoingRC4Decrypter = new RC4Engine();
                _OutgoingRC4Decrypter.Init(false, keyParam);

                var incomingEncrypter = new RC4Engine();
                incomingEncrypter.Init(true, keyParam);

                var outgoingEncrypter = new RC4Engine();
                outgoingEncrypter.Init(true, keyParam);

                _ClientConsumer = new ProxyPacketConsumer(_Client.GetStream(), incomingEncrypter, this);
                _ServerConsumer = new ProxyPacketConsumer(_Server.GetStream(), outgoingEncrypter, this);

                logger.WriteLine($"Packets are now encrypted");
                await consumer.Submit(packet, logger);
                return;
            }

            if (p.Signature.Equals(0x11, 0xc))
            {
                var packet = new TokenPacket(p, _RC4Key);
                logger.WriteLine($"{nameof(TokenPacket)} -> Token: {packet.Token.ToHexString()}", LoggerLevel.Verbose);
                await consumer.Submit(packet, logger);
                return;
            }

            if (p.Signature.Equals(0x11, 0x00))
            {
                var loginCredentials = new LoginCredentialsPacket(p);
                logger.WriteLine($"Login credentials -> User: `{loginCredentials.User}`");
                await consumer.Submit(p, logger);
                return;
            }

            if (p.Signature.Equals(0x11, 0x01))
            {
                var loginConfirmation = new LoginConfirmationPacket(p);

                if (loginConfirmation.Success)
                    logger.WriteLine($"Login succeeded -> UserId: {loginConfirmation.UserId}, BlockName: `{loginConfirmation.BlockName}`");
                else
                    logger.WriteLine($"Login failed");

                await consumer.Submit(p, logger);
                return;
                //var packet = new LoginConfirmationPacket(p);

                //// general sellout
                //var blockId = packet.BlockName.Substring(0, 5);
                //var releaseString =
                //packet.BlockName = $"{blockId}:{Program.ApplicationName}";

                //await consumer.Submit(packet, logger);
                //return;
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

                await consumer.Submit(packet, logger);

                return;
            }

            if (p.Signature.Equals(0x11, 0x13))
            {
                var packet = new BlockReplyPacket(p);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                await consumer.Submit(packet, logger);
                return;
            }

            // user rooms and team room both use the same packet structure
            if (p.Signature.Equals(0x11, 0x17) || p.Signature.Equals(0x11, 0x4f))
            {
                var packet = new RoomInfoPacket(p);
                logger.WriteLine($"{nameof(RoomInfoPacket)} -> Address: {packet.Address}, Port: {packet.Port}", LoggerLevel.Verbose);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                await consumer.Submit(packet, logger);
                return;
            }

            if (p.Signature.Equals(0x11, 0x21))
            {
                var packet = new SharedShipPacket(p);
                logger.WriteLine($"{nameof(SharedShipPacket)} -> Address: {packet.Address}, Port: {packet.Port}", LoggerLevel.Verbose);

                await _ProxyListenerManager.StartListenerAsync(packet.Address, packet.Port);
                packet.Address = _ApplicationResources.HostAddress;

                await consumer.Submit(packet, logger);
                return;
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

            // if there is no handler just pass through the packet without changing it
            await consumer.Submit(p, logger);
        }
    }
}
