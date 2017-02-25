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
            var userBytes = Body.Skip(500).Take(64).ToArray();
            User = Encoding.UTF8.GetString(userBytes).TrimEnd('\0');

            // ignore password
            //var passBytes = Body.Skip(564).Take(64).ToArray();
            //Password = Encoding.UTF8.GetString(passBytes).TrimEnd('\0');
        }
    }
}
