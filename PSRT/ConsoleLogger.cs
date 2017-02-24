using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    class ConsoleLogger : ILogger
    {
        private LoggerLevel _Level;

        public ConsoleLogger(LoggerLevel level = LoggerLevel.Info)
        {
            _Level = level;
        }

        public void WriteLine(string message, LoggerLevel level = LoggerLevel.Info)
        {
            if (level <= _Level)
                Console.WriteLine(message);
        }
    }
}
