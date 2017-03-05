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
        public bool Success;
        public uint UserId;

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
            // zero is success
            Success = BitConverter.ToUInt32(packet.Body, 0) == 0;

            // if not succeeded, packet is constructed differently
            if (!Success)
                return;

            UserId = BitConverter.ToUInt32(packet.Body, 8);

            _BlockName = Encoding.Unicode.GetString(Body, 20, 64).Split('\0')[0];
        }
    }
}
