using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class LuaPacket : Packet
    {
        public string LuaString;

        public LuaPacket(Packet packet) : base(packet)
        {
            var stringLength = checked((int)BitConverter.ToUInt32(Body, 4));

            var stringBytes = Body.Skip(8).Take(stringLength).ToArray();
            LuaString = Encoding.UTF8.GetString(stringBytes);
        }

        public LuaPacket(string luaString) : base(new PacketSignature { Type = 0x10, Subtype = 0x00 }, 0x0, null)
        {
            LuaString = luaString;
        }

        public override byte[] ToBytes()
        {
            var construction = new List<byte>();

            var stringBytes = Encoding.UTF8.GetBytes(LuaString);

            construction.AddRange(Enumerable.Repeat<byte>(0, 4));
            construction.AddRange(BitConverter.GetBytes(checked((uint)LuaString.Length)));
            construction.AddRange(stringBytes);
            construction.AddRange(Enumerable.Repeat<byte>(0, 4));
            construction.AddRange(Enumerable.Repeat<byte>(0, 4 - construction.Count % 4)); // alignment

            Body = construction.ToArray();

            return base.ToBytes();
        }
    }
}
