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
        byte[] _RC4Key;

        enum ProcessDirection
        {
            Incoming,
            Outgoing
        }

        public async Task RunAsync(TcpClient client, TcpClient server)
        {
            Console.WriteLine($"[{GetHashCode()}] Proxy start");

            var tasks = new Task[]
            {
                _ProcessClientAsync(client, server, ProcessDirection.Outgoing),
                _ProcessClientAsync(server, client, ProcessDirection.Incoming)
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{GetHashCode()}] Exception in proxy -> {ex.Message}");
            }

            Console.WriteLine($"[{GetHashCode()}] Proxy end");
        }

        async Task _ProcessClientAsync(TcpClient input, TcpClient output, ProcessDirection direction)
        {
            var packetBuffer = new List<byte>();
            var readBuffer = new byte[4096]; // pretty sure pso2 packets dont get bigger than this

            while (true)
            {
                byte[] key;

                try
                {
                    var count = await input.GetStream().ReadAsync(readBuffer, 0, readBuffer.Length);
                    if (count == 0)
                        break;

                    Console.WriteLine($"[{GetHashCode()}] [{direction}] Data length -> {count}");

                    key = _RC4Key;

                    if (key != null)
                        packetBuffer.AddRange(RC4.Decrypt(key, readBuffer.Take(count).ToArray()));
                    else
                        packetBuffer.AddRange(readBuffer.Take(count));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{GetHashCode()}] [{direction}] Error reading from stream -> {ex.Message}");
                    break;
                }

                if (packetBuffer.Count < 8)
                    // not enough for valid header construction
                    continue;

                var packetLength = BitConverter.ToInt32(packetBuffer.Take(4).ToArray(), 0);
                Console.WriteLine($"[{GetHashCode()}] [{direction}] Packet length -> {packetLength}");
                if (packetBuffer.Count < packetLength)
                    // whole packet not here yet
                    continue;

                var header = new PacketHeader
                {
                    Alpha = packetBuffer[4],
                    Beta = packetBuffer[5]
                };

                var packet = new Packet(header, packetBuffer.Skip(8).Take(packetLength - 8).ToArray());

                // remove packet data from buffer
                packetBuffer.RemoveRange(0, packetLength);

                Console.WriteLine($"[{GetHashCode()}] [{direction}] Packet -> Signature: {packet.Header}, Length: {packetLength}");

                var processed = _ProcessPacket(packet);

                var bytes = processed.ToBytes();
                if (key != null)
                    bytes = RC4.Encrypt(key, bytes);

                try
                {
                    await output.GetStream().WriteAsync(bytes, 0, bytes.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{GetHashCode()}] [{direction}] Error writing to stream -> {ex.Message}");
                    break;
                }
            }

            // close everything, this will kill off the other task also
            input.Close();
            output.Close();
        }

        Packet _ProcessPacket(Packet p)
        {
            if (p.Header.Equals(0x11, 0x2c))
            {
                var packet = new BlockInfoPacket(p);
                Console.WriteLine($"[{GetHashCode()}] {nameof(BlockInfoPacket)} -> Address: {packet.Address}, Port: {packet.Port}");

                Program.ListenForProxy(packet.Address, packet.Port);
                packet.Address = IPAddress.Loopback;

                return packet;
            }

            if (p.Header.Equals(0x11, 0xb))
            {
                var packet = new KeyExchangePacket(p);

                Console.WriteLine($"[{GetHashCode()}] {nameof(KeyExchangePacket)} -> Token: {packet.Token.ToHexString()}, RC4 key: {packet.RC4Key.ToHexString()}");

                _RC4Key = packet.RC4Key;
                Console.WriteLine($"[{GetHashCode()}] Packets are now encrypted");

                return packet;
            }

            if (p.Header.Equals(0x11, 0xc))
            {
                var packet = new TokenPacket(p, _RC4Key);

                Console.WriteLine($"[{GetHashCode()}] {nameof(TokenPacket)} -> Token: {packet.Token.ToHexString()}");

                return packet;
            }

            if (p.Header.Equals(0x11, 0x0))
            {
                var packet = new LoginPacket(p);
                Console.WriteLine($"[{GetHashCode()}] LoginPacket -> User: `{packet.User}`");
                return packet;
            }

            return p;
        }
    }
}
