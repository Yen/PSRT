using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class LoginPacket : Packet
    {
        public string User { get; private set; }
        //public string Password { get; private set; }

        public LoginPacket(Packet packet) : base(packet)
        {
            var credentialsOffset = Body.Length - 132;

            var userBytes = Body.Skip(credentialsOffset).Take(64).ToArray();
            User = Encoding.ASCII.GetString(userBytes).Split('\0')[0];

            // ignore password
            //var passBytes = Body.Skip(credentialsOffset + 64).Take(64).ToArray();
            //Password = Encoding.ASCII.GetString(passBytes).Split('\0')[0];
        }
    }
}
