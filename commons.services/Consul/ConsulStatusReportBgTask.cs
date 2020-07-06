using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Consul;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace commons.services.Consul
{
    public class ConsulStatusReportBgTask : BackgroundService
    {
        private readonly IConsulClient _consulClient;
        private readonly ILogger<ConsulStatusReportBgTask> _logger;
        private readonly IServer _server;
        private readonly IConfiguration _configuration;
        private readonly ConsulServiceConguration _serviceConfuration;
        private Timer _timer;
        private string _serviceId;
        private string _checkId;

        public ConsulStatusReportBgTask(IConsulClient consulClient, ILogger<ConsulStatusReportBgTask> logger,
            IServer server, IConfiguration configuration, ConsulServiceConguration serviceConguration)
        {
            _consulClient = consulClient;
            _logger = logger;
            _server = server;
            _configuration = configuration;
            _serviceConfuration = serviceConguration;
        }

        private CancellationTokenSource _cts;


        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();

            _timer?.Dispose();
            _logger.LogInformation($"Deregistering service {_serviceConfuration.ServiceName} from Consul");
            try
            {
                await _consulClient.Agent.ServiceDeregister(_serviceId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Deregisteration service {_serviceConfuration.ServiceName} failed");
            }
            _logger.LogInformation($"consul status report bg task stopping");
        }

        protected void DoWork(object state)
        {
            ThreadPool.QueueUserWorkItem(async (s) =>
            {
                await _consulClient.Agent.UpdateTTL(_checkId, "SERVING", TTLStatus.Pass, _cts.Token);
            });

        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var features = _server.Features;
            var addresses = features.Get<IServerAddressesFeature>().Addresses;
            string address;
            if (_serviceConfuration.ServiceUrl != "")
            {
                address = _serviceConfuration.ServiceUrl;
            }
            else
            {
                address = addresses.First();
            }
            var uri = new Uri(address);

            _serviceId = $"Service-v1-{Dns.GetHostName()}-{_serviceConfuration.ServiceName}-{uri.Authority}";

            var registration = new AgentServiceRegistration()
            {
                ID = _serviceId,
                Name = _serviceConfuration.ServiceName,
                Address = uri.Host,
                Port = uri.Port,
                Tags = _serviceConfuration.Tags != null ? _serviceConfuration.Tags : new string[0],
                Meta = new Dictionary<string, string>(){ { "scheme", "https" } },
                Check = new AgentServiceCheck()
                {
                    TTL = TimeSpan.FromSeconds(_serviceConfuration.TTLSeconds),
                    DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(20 * _serviceConfuration.TTLSeconds),
                    // HTTP = $"{uri.Scheme}://{uri.Host}:{uri.Port}/api/Health/Status",
                    //GRPC = $"{uri.Host}:{uri.Port}",
                    Timeout = TimeSpan.FromSeconds(_serviceConfuration.TimeoutSeconds),
                    // Interval = TimeSpan.FromSeconds(10)
                }
            };
            try
            {
                // 首先移除服务，避免重复注册
                await _consulClient.Agent.ServiceDeregister(registration.ID, _cts.Token);
                await _consulClient.Agent.ServiceRegister(registration, _cts.Token);

                var checks = await _consulClient.Agent.Checks();
                foreach (var checkPair in checks.Response)
                {
                    if (checkPair.Value.ServiceID == _serviceId)
                    {
                        _checkId = checkPair.Key;
                        break;
                    }
                }
                if (_checkId == null)
                {
                    _logger.LogError($"Can't find check of service {_serviceId}");
                }

                _logger.LogInformation($"registered service {_serviceConfuration.ServiceName}");

                _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            }
            catch (Exception e)
            {
                _logger.LogError($"consul service {_serviceConfuration.ServiceName} register error {e.Message}");
            }
        }
    }
}
