using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BusinessApi.Sagas;
using Consul;
using BusinessApi.Tasks;
using commons.services.Sagas;

namespace BusinessApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<OrderService>();
            services.AddSingleton<CreateOrderSaga>();
            services.AddSingleton<SagaWorker, SimpleSagaWorker>();
            services.AddSingleton<SagaStore, MemorySagaStore>();

            services.AddSingleton<GrpcClientsHolder>();

            var consulAddress = Configuration.GetSection("Consul")["ConsulUrl"];
            services.AddSingleton<IConsulClient>(new ConsulClient(o => o.Address = new Uri(consulAddress)));

            services.AddHostedService<InitServicesBgTask>();
            services.AddHostedService<SagaWorkerBgTask>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
