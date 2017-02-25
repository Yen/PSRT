using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class BlockReplyPacket : Packet
    {
        private IPAddress _Address;
        public IPAddress Address
        {
            get => _Address;
            set
            {
                Array.Copy(value.GetAddressBytes(), 0, Body, 12, 4);
                _Address = value;
            }
        }

        private int _Port;
        public int Port
        {
            get => _Port;
            set
            {
                Array.Copy(BitConverter.GetBytes(checked((ushort)value)), 0, Body, 16, 2);
                _Port = value;
            }
        }

        public BlockReplyPacket(Packet packet) : base(packet)
        {
            _Address = new IPAddress(Body.Skip(12).Take(4).ToArray());
            _Port = BitConverter.ToUInt16(Body, 16);
        }
    }
}
