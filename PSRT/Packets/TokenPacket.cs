using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class TokenPacket : Packet
    {
        public byte[] Token { get; private set; }

        public TokenPacket(Packet packet, byte[] RC4Key) : base(packet)
        {
            Token = RC4.Decrypt(RC4Key, packet.Body.ToArray());
        }
    }
}
