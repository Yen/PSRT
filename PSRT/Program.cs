using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PSRT
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"PSRT {Assembly.GetExecutingAssembly().GetName().Version}");

            ListenForProxy(IPAddress.Parse("210.189.208.16"), 12200).Wait();
        }

        public static Task ListenForProxy(IPAddress proxyAddress, int port)
        {
            Console.WriteLine($"Starting proxy listener -> {proxyAddress}:{port}");
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Console.WriteLine($"Listening on port `{port}`");

            return Task.Run(() =>
            {
                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    Console.WriteLine($"Accepted proxy client on port `{port}`");

                    var server = new TcpClient();
                    server.Connect(proxyAddress, port);

                    var proxy = new Proxy(new ConsoleLogger(LoggerLevel.Verbose));
                    Task.Run(async () => await proxy.RunAsync(client, server).ContinueWith((t) =>
                    {
                        client.Close();
                        server.Close();
                    }));
                }
            });
        }

        public static string ToHexString(this byte[] bytes)
        {
            return $"[{ string.Join(", ", bytes.Select(x => $"0x{x.ToString("x")}"))}]";
        }
    }

}
