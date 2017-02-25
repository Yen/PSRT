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
        public List<BlockInfo> BlockInfos { get; private set; } = new List<BlockInfo>();

        public BlockListPacket(Packet packet) : base(packet.Signature, packet.Body)
        {
            // TODO: PSO2Proxy method

            var position = 20;
            while (position < Body.Length && Body[position] != 0)
            {
                var name = Encoding.Unicode.GetString(Body, position, 64).TrimEnd('\0');
                var address = new IPAddress(Body.Skip(position).Skip(64).Take(4).ToArray());
                var port = checked((int)BitConverter.ToUInt16(Body, position + 68));

                BlockInfos.Add(new BlockInfo
                {
                    Name = name,
                    Address = address,
                    Port = port
                });

                // shift to next block
                position += 232;
            }
        }

        public override byte[] ToBytes()
        {
            for (int i = 0; i < BlockInfos.Count; i++)
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
