using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class BlockInfoPacket : Packet
    {
        IPAddress _Address;
        int _Port;
        string _Name;

        public IPAddress Address
        {
            get => _Address;
            set
            {
                Array.Copy(value.GetAddressBytes(), 0, Body, 96, 4);
                _Address = value;
            }
        }

        public int Port
        {
            get => _Port;
            set
            {
                Array.Copy(BitConverter.GetBytes((ushort)value), 0, Body, 100, 2);
                _Port = value;
            }
        }

        public string Name
        {
            get => _Name;
            set
            {
                Array.Copy(Encoding.Unicode.GetBytes(value), 0, Body, 32, 64);
                _Name = value;
            }
        }

        public BlockInfoPacket(Packet packet) : base(packet.Header, packet.Body)
        {
            _Address = new IPAddress(Body.Skip(96).Take(4).ToArray());
            _Port = BitConverter.ToUInt16(Body, 100);
            _Name = Encoding.Unicode.GetString(Body, 32, 64).TrimEnd('\0');
        }
    }
}
