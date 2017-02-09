using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    struct PacketHeader
    {
        public byte Alpha;
        public byte Beta;

        public override string ToString()
        {
            return $"[0x{Alpha.ToString("x")}, 0x{Beta.ToString("x")}]";
        }

        public bool Equals(byte alpha, byte beta)
        {
            return alpha == Alpha && beta == Beta;
        }
    }

    class Packet
    {
        public PacketHeader Header;
        public byte[] Body;

        public Packet(PacketHeader header, byte[] body)
        {
            Header = header;
            Body = body;
        }

        public byte[] ToBytes()
        {
            var bytes = new byte[Body.Length + 8];
            Array.Copy(Body, 0, bytes, 8, Body.Length);

            var packetSize = BitConverter.GetBytes(Body.Length + 8);
            Array.Copy(packetSize, bytes, 4);

            bytes[4] = Header.Alpha;
            bytes[5] = Header.Beta;

            Array.Clear(bytes, 6, 2);

            return bytes;
        }
    }
}
