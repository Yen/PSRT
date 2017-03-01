using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT
{
    class ProxyListenerManager
    {
        private struct ListenerInfo
        {
            public IPAddress Address;
            public int Port;
            public Task RunnerTask;

            public class ProxyInfo
            {
                public Proxy Proxy;
                public Task RunnerTask;
            }

            public List<ProxyInfo> Proxies;
        }

        private ApplicationResources _ApplicationResources;
        private ILogger _Logger;
        private ILogger _ProxyLogger;
        private SemaphoreSlim _ListenerSemaphore = new SemaphoreSlim(1, 1);
        private List<ListenerInfo> _Listeners = new List<ListenerInfo>();

        public ProxyListenerManager(ILogger logger, ApplicationResources applicationResources)
        {
            _Logger = new StagedLogger(logger, nameof(ProxyListenerManager));
            _ProxyLogger = logger;
            _ApplicationResources = applicationResources;
        }

        public async Task<Task> StartListenerAsync(IPAddress address, int port)
        {
            await _ListenerSemaphore.WaitAsync();
            try
            {
                var listenLocation = Tuple.Create(address, port);

                // do not start new listener if there is already one running on that port,
                // this does not take into account same ports on different addresses but
                // I dont know if pso2 does that or not
                if (_Listeners.Any(x => x.Port == port))
                    return _Listeners.First(x => x.Port == port).RunnerTask;

                _Logger.WriteLine($"Starting listener -> {address} : {port}");

                var proxies = new List<ListenerInfo.ProxyInfo>();

                var listenerTask = Task.Run(async () =>
                {
                    var listener = new TcpListener(_ApplicationResources.BindAddress, port);
                    try
                    {
                        listener.Start();
                        while (true)
                        {
                            var client = await listener.AcceptTcpClientAsync();

                            _Logger.WriteLine("Accepted new client");

                            var server = new TcpClient();
                            await server.ConnectAsync(address, port);

                            var proxy = new Proxy(client, server, _ProxyLogger, _ApplicationResources, this);
                            var proxyInfo = new ListenerInfo.ProxyInfo { Proxy = proxy };
                            proxies.Add(proxyInfo);

                            var proxyTask = Task.Run(proxy.RunAsync).ContinueWith(t =>
                            {
                                proxies.Remove(proxyInfo);
                            });

                            proxyInfo.RunnerTask = proxyTask;
                        }
                    }
                    finally
                    {
                        listener.Stop();

                        await _ListenerSemaphore.WaitAsync();
                        try
                        {
                            _Listeners.RemoveAll(x => x.Port == port);
                        }
                        finally
                        {
                            _ListenerSemaphore.Release();
                        }
                    }
                });

                _Listeners.Add(new ListenerInfo
                {
                    Address = address,
                    Port = port,
                    RunnerTask = listenerTask,
                    Proxies = proxies
                });

                return listenerTask;
            }
            finally
            {
                _ListenerSemaphore.Release();
            }
        }
    }
}
