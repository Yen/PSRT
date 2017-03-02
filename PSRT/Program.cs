using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT
{
    static class Program
    {
#if DEBUG
        public static string ApplicationName = $"PSRT Debug {Assembly.GetExecutingAssembly().GetName().Version}";
#else
        public static string ApplicationName = $"PSRT {Assembly.GetExecutingAssembly().GetName().Version}";
#endif
        
        static void Main(string[] args)
        {
            Console.WriteLine(ApplicationName);

            Task.Run(MainAsync).Wait();
        }

        static async Task MainAsync()
        {
            //var logger = new ConsoleLogger(LoggerLevel.Verbose);
            var logger = new ConsoleLogger();

            var applicationResources = new ApplicationResources();
            try
            {
                await applicationResources.LoadResources();
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Error loading application resources -> {ex.Message}");
                return;
            }

            var listenerManager = new ProxyListenerManager(logger, applicationResources);

            // ship2
            // await await is a good pattern
            await await listenerManager.StartListenerAsync(IPAddress.Parse("210.189.208.16"), 12200);
        }

        public static string ToHexString(this byte[] bytes)
        {
            return $"[{string.Join(", ", bytes.Select(x => $"0x{x.ToString("x")}"))}]";
        }

        private static int _PacketDumpIndex = 0;
        public static void DumpPacket(Packet packet)
        {
            Directory.CreateDirectory("PacketDump");
            File.WriteAllBytes($"PacketDump/{Interlocked.Increment(ref _PacketDumpIndex)} {packet.Signature} 0x{packet.Flags.ToString("x4")}", packet.ToBytes());
        }
    }

}
