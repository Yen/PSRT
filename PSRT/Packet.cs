using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    struct PacketSignature
    {
        public byte Type;
        public byte Subtype;

        public override string ToString()
        {
            return $"[0x{Type.ToString("x")}, 0x{Subtype.ToString("x")}]";
        }

        public bool Equals(byte type, byte subtype)
        {
            return type == Type && subtype == Subtype;
        }
    }

    class Packet
    {
        public PacketSignature Signature;
        public ushort Flags;
        public byte[] Body;

        public Packet(Packet copy): this(copy.Signature, copy.Flags, copy.Body)
        { }

        public Packet(PacketSignature signature, ushort flags, byte[] body)
        {
            Signature = signature;
            Flags = flags;
            Body = body;
        }

        public virtual byte[] ToBytes()
        {
            var length = Body.Length + 8;
            var result = new byte[length];

            // length 0-3
            Array.Copy(BitConverter.GetBytes(length), result, 4);

            // signature 4-5
            result[4] = Signature.Type;
            result[5] = Signature.Subtype;

            // flags 6-7
            Array.Copy(BitConverter.GetBytes(Flags), 0, result, 6, 2);

            // body 8-...
            Array.Copy(Body, 0, result, 8, Body.Length);

            return result;
        }
    }
}
