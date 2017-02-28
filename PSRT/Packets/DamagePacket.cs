using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Packets
{
    class DamagePacket : Packet
    {
        public enum DamageTypeFlags : byte
        {
            JustAttack = 1,
            Misc = 4,
            Damage = 8,
            MultiHit = 16,
            Misc2 = 32,
            Critical = 64
        }

        public uint Account { get; private set; }

        public uint Target { get; private set; }

        public uint Instance { get; private set; }

        public uint Source { get; private set; }

        public uint Action { get; private set; }

        public int Value { get; private set; }

        public DamageTypeFlags DamageType { get; private set; }

        public DamagePacket(Packet packet) : base(packet)
        {
            Account = BitConverter.ToUInt32(Body, 0);
            
            Target = BitConverter.ToUInt32(Body, 12);
            
            Instance = BitConverter.ToUInt32(Body, 22);
            
            Source = BitConverter.ToUInt32(Body, 28);
            
            Action = BitConverter.ToUInt32(Body, 40);
            
            Value = BitConverter.ToInt32(Body, 44);
            
            DamageType = checked((DamageTypeFlags)Body[64]);
        }
    }
}
