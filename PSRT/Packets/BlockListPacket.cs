using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class BlockInfo
    {
        public string Name;
        public IPAddress Address;
        public int Port;
    }

    class BlockListPacket : Packet
    {
        // currently no way to resize block count
        public BlockInfo[] BlockInfos { get; private set; }

        public BlockListPacket(Packet packet) : base(packet)
        {
            var blockCount = BitConverter.ToInt32(Body, 0);
            BlockInfos = new BlockInfo[blockCount];

            for (int i = 0; i < blockCount; i++)
            {
                var offset = 20 + i * 232;

                var name = Encoding.Unicode.GetString(Body, offset, 64).Split('\0')[0];
                var address = new IPAddress(Body.Skip(offset + 64).Take(4).ToArray());
                var port = checked((int)BitConverter.ToUInt16(Body, offset + 68));

                BlockInfos[i] = new BlockInfo
                {
                    Name = name,
                    Address = address,
                    Port = port
                };
            }
        }

        public override byte[] ToBytes()
        {
            for (int i = 0; i < BlockInfos.Length; i++)
            {
                var position = 20 + (232 * i);

                var nameBytes = Encoding.Unicode.GetBytes(BlockInfos[i].Name);
                Array.Clear(Body, position, 64);
                Array.Copy(nameBytes, 0, Body, position, Math.Min(64, nameBytes.Length));

                Array.Copy(BlockInfos[i].Address.GetAddressBytes(), 0, Body, position + 64, 4);
                Array.Copy(BitConverter.GetBytes(checked((ushort)BlockInfos[i].Port)), 0, Body, position + 64 + 4, 2);
            }

            return base.ToBytes();
        }
    }
}
