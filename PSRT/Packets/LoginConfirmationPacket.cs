using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class LoginConfirmationPacket : Packet
    {
        private string _BlockName;
        public string BlockName
        {
            get => _BlockName;
            set
            {
                var buffer = Encoding.Unicode.GetBytes(value);
                Array.Clear(Body, 20, 64);
                Array.Copy(buffer, 0, Body, 20, Math.Min(64, buffer.Length));
                _BlockName = value;
            }
        }

        public LoginConfirmationPacket(Packet packet) : base(packet)
        {
            _BlockName = Encoding.Unicode.GetString(Body, 20, 64).TrimEnd('\0');
        }
    }
}
