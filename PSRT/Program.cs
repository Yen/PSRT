using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"PSRT {Assembly.GetExecutingAssembly().GetName().Version}");

            Task.Run(MainAsync).Wait();

            // TODO: infinite wait
            new ManualResetEventSlim().Wait();
        }

        static async Task MainAsync()
        {
#if DEBUG
            var logger = new ConsoleLogger(LoggerLevel.Verbose);
#else
            var logger = new ConsoleLogger();
#endif
            var listenerManager = new ProxyListenerManager(logger);
            await listenerManager.StartListenerAsync(IPAddress.Parse("210.189.208.16"), 12200);
        }

        public static string ToHexString(this byte[] bytes)
        {
            return $"[{ string.Join(", ", bytes.Select(x => $"0x{x.ToString("x")}"))}]";
        }
    }

}
