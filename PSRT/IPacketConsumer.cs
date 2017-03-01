using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    interface IPacketConsumer
    {
        Task Submit(Packet packet);
    }
}
