using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class RoomInfoPacket : Packet
    {
        IPAddress _Address;
        int _Port;

        public IPAddress Address
        {
            get => _Address;
            set
            {
                Array.Copy(value.GetAddressBytes(), 0, Body, 24, 4);
                _Address = value;
            }
        }

        public int Port
        {
            get => _Port;
            set
            {
                Array.Copy(BitConverter.GetBytes((ushort)value), 0, Body, 32, 2);
                _Port = value;
            }
        }

        public RoomInfoPacket(Packet packet) : base(packet.Signature, packet.Body)
        {
            _Address = new IPAddress(Body.Skip(24).Take(4).ToArray());
            _Port = BitConverter.ToUInt16(Body, 32);
        }
    }
}
