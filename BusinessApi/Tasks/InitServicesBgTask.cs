using Consul;
using customer_service;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using merchant_service;
using order_service;
using history_service;
using System.Net.Http;

namespace BusinessApi.Tasks
{
    public class InitServicesBgTask : BackgroundService
    {
        private readonly ILogger<InitServicesBgTask> _logger;
        private readonly GrpcClientsHolder _grpcClientsHolder;
        private readonly IConsulClient _consulClient;

        public InitServicesBgTask(ILogger<InitServicesBgTask> logger, GrpcClientsHolder grpcClientsHolder,
            IConsulClient consulClient)
        {
            this._logger = logger;
            this._grpcClientsHolder = grpcClientsHolder;
            this._consulClient = consulClient;
        }

        private async Task<string> getGrpcServiceEndpoint(string serviceName, string scheme="https")
        {
            var sResp = await _consulClient.Catalog.Service(serviceName);
            if (sResp.Response.Count() < 1)
            {
                throw new Exception($"can't find service {serviceName}");
            }
            var s = sResp.Response.First();
            if (s.ServiceMeta.ContainsKey("scheme"))
            {
                scheme = s.ServiceMeta["scheme"];
            }
            var url = $"{scheme}://{s.ServiceAddress}:{s.ServicePort}";
            return url;
        }

        private ChannelBase createGrpcChannelFromUrl(string url)
        {
            if (url.StartsWith("https://"))
            {
                var httpHandler = new HttpClientHandler();
                // Return `true` to allow certificates that are untrusted/invalid
                httpHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                return GrpcChannel.ForAddress(url, new GrpcChannelOptions { HttpHandler = httpHandler });
            }
            else if (url.StartsWith("http://"))
            {
                return GrpcChannel.ForAddress(url, new GrpcChannelOptions() { Credentials = ChannelCredentials.Insecure });
            }
            else if (url.StartsWith("grpc://"))
            {
                var uri = new Uri(url);
                var endpoint = $"{uri.Host}:{uri.Port}";
                return new Channel(endpoint, ChannelCredentials.Insecure);
            }
            else
            {
                var uri = new Uri(url);
                var endpoint = $"{uri.Host}:{uri.Port}";
                return new Channel(endpoint, ChannelCredentials.Insecure);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                {
                    var serviceName = "OrderService";
                    var url = await getGrpcServiceEndpoint(serviceName);
                    var channel = createGrpcChannelFromUrl(url);
                    _grpcClientsHolder.OrderClient = new Order.OrderClient(channel);
                }
                {
                    var serviceName = "CustomerService";
                    var url = await getGrpcServiceEndpoint(serviceName);
                    _logger.LogInformation($"service {serviceName} url {url}");
                    var channel = createGrpcChannelFromUrl(url);
                    _grpcClientsHolder.CustomerClient = new Customer.CustomerClient(channel);
                }
                {
                    var serviceName = "MerchantService";
                    var url = await getGrpcServiceEndpoint(serviceName);
                    _logger.LogInformation($"service {serviceName} url {url}");
                    var channel = createGrpcChannelFromUrl(url);
                    _grpcClientsHolder.MerchantClient = new Merchant.MerchantClient(channel);
                }
                {
                    var serviceName = "history.service";
                    var url = await getGrpcServiceEndpoint(serviceName, "grpc");
                    _logger.LogInformation($"service {serviceName} url {url}");
                    var channel = createGrpcChannelFromUrl(url);
                    _grpcClientsHolder.HistoryClient = new History.HistoryClient(channel);
                }
                _logger.LogInformation($"grpc services clients inited");
            }
            catch (Exception e)
            {
                _logger.LogError($"init services error {e.Message}");
                throw e;
            }
        }
    }
}
