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
using commons.services.Saga;

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

            services.AddSagaService<OrderServiceImpl>();
            services.AddSingleton<IOrderService, OrderServiceSagaProxy>();

            services.AddSaga<CreateOrderSaga>();

            var consulAddress = Configuration.GetSection("Consul")["ConsulUrl"];
            services.AddSingleton<IConsulClient>(new ConsulClient(o => o.Address = new Uri(consulAddress)));
            
            services.AddSagaConfig(Configuration);

            services.AddHostedService<InitServicesBgTask>();
            services.AddSingleton<GrpcClientsHolder>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env,
            ILogger<Startup> logger)
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

            // 把各saga服务都自动加载，从而可以从saga server监听各补偿任务
            var serviceProvider = app.ApplicationServices;
            var allSagas = serviceProvider.LoadSagaTypes();
            logger.LogInformation($"loaded {allSagas.Count()} sagas");
        }
    }
}
