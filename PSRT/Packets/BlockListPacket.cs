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

        public BlockListPacket(Packet packet) : base(packet.Header, packet.Body)
        {
            // TODO: PSO2Proxy method

            var position = 20;
            while (position < Body.Length && Body[position] != 0)
            {
                var name = Encoding.Unicode.GetString(Body, position, 64).TrimEnd('\0');

                // temp rename
                //var newNameBytes = Encoding.Unicode.GetBytes("Ayy");
                //Array.Clear(Body, position, 64);
                //Array.Copy(newNameBytes, 0, Body, position, Math.Min(64, newNameBytes.Length));
                //

                var address = new IPAddress(Body.Skip(64).Take(4).ToArray());
                var port = checked((int)BitConverter.ToUInt16(Body, 68));

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
    }
}
