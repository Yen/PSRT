using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class SharedShipPacket : Packet
    {
        IPAddress _Address;
        int _Port;

        public IPAddress Address
        {
            get => _Address;
            set
            {
                Array.Copy(value.GetAddressBytes(), Body, 4);
                _Address = value;
            }
        }

        public int Port
        {
            get => _Port;
            set
            {
                Array.Copy(BitConverter.GetBytes((ushort)value), 0, Body, 4, 2);
                _Port = value;
            }
        }

        public SharedShipPacket(Packet packet) : base(packet.Signature, packet.Body)
        {
            _Address = new IPAddress(Body.Take(4).ToArray());
            _Port = BitConverter.ToUInt16(Body, 4);
        }
    }
}
