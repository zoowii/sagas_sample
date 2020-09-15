using commons.services.Saga;
using commons.services.Sagas;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SagaConfigServiceCollectionExtensions
    {
        private static List<Type> simpleSagaTypes = new List<Type>();
        private static List<Type> sagaServicesTypes = new List<Type>();

        public static IServiceCollection AddSaga<T>(this IServiceCollection services) where T : class, ISaga
        {
            services.AddSingleton<T>();
            simpleSagaTypes.Add(typeof(T));
            return services;
        }

        public static IServiceCollection AddSaga<Interface, RealClass, ProxyClass>(this IServiceCollection services)
            where RealClass : class, ISaga, Interface 
            where ProxyClass : class, ISaga, Interface
            where Interface: class
        {
            services.AddSingleton<RealClass>();
            simpleSagaTypes.Add(typeof(RealClass));
            services.AddSingleton<Interface, ProxyClass>();
            return services;
        }

        public static IServiceCollection AddSagaService<T>(this IServiceCollection services) where T : class, SagaService
        {
            services.AddSingleton<T>();
            sagaServicesTypes.Add(typeof(T));
            return services;
        }

        public static IServiceCollection AddSagaConfig(
             this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<ISagaDataConverter, JsonSagaDataConverter>();
            services.AddSingleton<ISagaResolver, SimpleSagaResolver>();
            // services.AddSingleton<SagaWorker, SimpleSagaWorker>();
            // services.AddSingleton<SagaStore, MemorySagaStore>();


            var sagaNodeConfigSection = config.GetSection("Saga:Node");
            if (sagaNodeConfigSection.Exists())
            {
                var sagaNodeInfoConfig = sagaNodeConfigSection.Get<SagaNodeInfoConfig>();
                services.AddSingleton(sagaNodeInfoConfig);
            }
            else
            {
                var sagaNodeInfoConfig = new SagaNodeInfoConfig()
                {
                    Group = "annoy",
                    Service = "annoy",
                    InstanceId = "0"
                };
                services.AddSingleton(sagaNodeInfoConfig);
            }


            services.AddSingleton<SagaCollaborator>();
            services.AddSingleton<CollaboratorSagaWorker>();

            // services.AddHostedService<SagaWorkerBgTask>(); 这是完全预先申明式saga定义的worker
            services.AddHostedService<CollaboratorSagaWorkerBgTask>();

            return services;
        }

        public static IEnumerable<Type> GetSagaTypes(this IServiceProvider serviceProvider)
        {
            return simpleSagaTypes;
        }


        // 在DI中加载各注册的ISaga的实现类型
        public static IEnumerable<ISaga> LoadSagaTypes(this IServiceProvider serviceProvider)
        {
            // 预先加载一些saga相关基础类
            serviceProvider.GetService(typeof(ISagaDataConverter));
            serviceProvider.GetService(typeof(ISagaResolver));

            var sagaTypes = serviceProvider.GetSagaTypes();

            var allSagas = from t in sagaTypes select serviceProvider.GetService(t) as ISaga;
            return allSagas;
        }
    }
}
