using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    interface ILogger
    {
        void WriteLine(string message, LoggerLevel level = LoggerLevel.Info);
    }

    enum LoggerLevel
    {
        Info,
        Verbose
    }
}
