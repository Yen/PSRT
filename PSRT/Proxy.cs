using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using PSRT.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    class Proxy
    {
        private ILogger _BaseLogger;

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

        public Proxy(ILogger logger)
        {
            _BaseLogger = new StagedLogger(new StagedLogger(logger, nameof(Proxy)), GetHashCode().ToString());
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
                await _HandleConnectionInternal(input, output, direction, logger);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Exception in handler -> {ex.Message}", LoggerLevel.Verbose);
            }

            // ensure connections are teminated
            input.Close();
            output.Close();

            logger.WriteLine("Handler end");
        }

        private async Task _HandleConnectionInternal(TcpClient input, TcpClient output, ConnectionDirection direction, ILogger logger)
        {
            var acceptedBuffer = new List<byte>();
            var streamBuffer = new byte[4096];

            while (true)
            {
                try
                {
                    var count = await input.GetStream().ReadAsync(streamBuffer, 0, streamBuffer.Length);
                    if (count == 0)
                    {
                        logger.WriteLine("End of input stream reached", LoggerLevel.Verbose);
                        break;
                    }

                    logger.WriteLine($"Data received -> Length: {count}", LoggerLevel.Verbose);
                    
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
                }
                catch (Exception ex)
                {
                    logger.WriteLine($"Error reading from stream -> {ex.Message}", LoggerLevel.Verbose);
                    break;
                }

                // enough data to construct packet length info
                while (acceptedBuffer.Count >= 4)
                {
                    var packetLength = BitConverter.ToInt32(acceptedBuffer.ToArray(), 0);
                    logger.WriteLine($"Packet length received -> {packetLength}");

                    // not enough data to construct whole of packet, wait for more data
                    if (packetLength > acceptedBuffer.Count)
                        break;

                    var packetBuffer = acceptedBuffer.Take(packetLength).ToList();

                    var packetHeader = new PacketHeader
                    {
                        Alpha = packetBuffer[4],
                        Beta = packetBuffer[5]
                    };
                    logger.WriteLine($"Complete packet received -> Length: {packetLength}, Signature: {packetHeader}");

                    var headerBuffer = packetBuffer.Take(8).ToArray();
                    var bodyBuffer = packetBuffer.Skip(8).ToArray();

                    // remove packet data from buffer so it does not get processed again
                    acceptedBuffer.RemoveRange(0, packetLength);

                    //logger.WriteLine($"UTF-8 -> {Encoding.UTF8.GetString(bodyBuffer)}");
                    //logger.WriteLine($"UTF-16 -> {Encoding.Unicode.GetString(bodyBuffer)}");

                    // get encrypter early in case packet handling changes it
                    var encrypter = direction == ConnectionDirection.Incoming ? _IncomingRC4Encrypter : _OutgoingRC4Encrypter;

                    var p = _HandlePacket(new Packet(packetHeader, bodyBuffer), logger);

                    var responseBuffer = new byte[packetLength];
                    Array.Copy(headerBuffer, responseBuffer, 8);
                    Array.Copy(p.Body, 0, responseBuffer, 8, p.Body.Length);

                    if (encrypter != null)
                    {
                        var encrypted = new byte[responseBuffer.Length];
                        encrypter.ProcessBytes(responseBuffer, 0, responseBuffer.Length, encrypted, 0);
                        responseBuffer = encrypted;
                    }

                    try
                    {
                        await output.GetStream().WriteAsync(responseBuffer, 0, responseBuffer.Length);
                        await output.GetStream().FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.WriteLine($"Error writing to stream -> {ex.Message}", LoggerLevel.Verbose);
                        break;
                    }
                }
            }
        }

        private Packet _HandlePacket(Packet p, ILogger logger)
        {
            if (p.Header.Equals(0x11, 0x2c))
            {
                var packet = new BlockInfoPacket(p);
                logger.WriteLine($"{nameof(BlockInfoPacket)} -> Address: {packet.Address}, Port: {packet.Port}", LoggerLevel.Verbose);

                Program.ListenForProxy(packet.Address, packet.Port);
                packet.Address = IPAddress.Loopback;

                packet.Name = "Ayy lmao";

                return packet;
            }

            if (p.Header.Equals(0x11, 0xb))
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

                logger.WriteLine($"Packets are now encrypted", LoggerLevel.Verbose);
                return packet;
            }

            if (p.Header.Equals(0x11, 0xc))
            {
                var packet = new TokenPacket(p, _RC4Key);
                logger.WriteLine($"{nameof(TokenPacket)} -> Token: {packet.Token.ToHexString()}");
                return packet;
            }

            if (p.Header.Equals(0x11, 0x0))
            {
                var packet = new LoginPacket(p);
                logger.WriteLine($"LoginPacket -> User: `{packet.User}`", LoggerLevel.Verbose);
                return packet;
            }

            if (p.Header.Equals(0x11, 0x10))
            {
                var packet = new BlockListPacket(p);
                return packet;
            }

            return p;
        }
    }
}
