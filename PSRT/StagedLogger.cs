using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    class StagedLogger : ILogger
    {
        private ILogger _Output;
        private string _Tag;

        public StagedLogger(ILogger output, string tag)
        {
            _Output = output;
            _Tag = tag;
        }

        public void WriteLine(string message, LoggerLevel level = LoggerLevel.Info)
        {
            _Output.WriteLine($"[{_Tag}] {message}", level);
        }
    }
}
