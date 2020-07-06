using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Consul
{
    public static class ConsulServicesExtensions
    {
        public static IServiceCollection UseTTLConsul(this IServiceCollection services, IConfiguration configuration)
        {
            var consulServiceConfig = new ConsulServiceConguration();
            configuration.GetSection(ConsulServiceConguration.Section).Bind(consulServiceConfig);
            services.AddSingleton(consulServiceConfig);

            string consulAddress = consulServiceConfig.ConsulUrl;
            services.AddSingleton<IConsulClient>(new ConsulClient(o =>
            {
                o.Address = new Uri(consulAddress);
            }));

            services.AddHostedService<ConsulStatusReportBgTask>();
            return services;
        }
    }
}
