using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Reflection;

namespace DotNetAutoRegistration.PoC
{
    class Program
    {
        static void Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            ExemplifyScoping(host.Services, "Scope 1");
            ExemplifyScoping(host.Services, "Scope 2");

            host.Run();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                    services
                        //.AddTransient<ITransientOperation, TransientOperation>()
                        //.AddScoped<IScopedOperation, ScopedOperation>()
                        //.AddSingleton<ISingletonOperation, SingletonOperation>()
                        .AddServicesFromAppDomain()
                        .AddTransient<OperationLogger>());

        static void ExemplifyScoping(IServiceProvider services, string scope)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;

            OperationLogger logger = provider.GetRequiredService<OperationLogger>();
            logger.LogOperations($"{scope}-Call 1 .GetRequiredService<OperationLogger>()");

            Console.WriteLine("...");

            logger = provider.GetRequiredService<OperationLogger>();
            logger.LogOperations($"{scope}-Call 2 .GetRequiredService<OperationLogger>()");

            Console.WriteLine();
        }
    }

    public interface IOperation
    {
        string OperationId { get; }
    }

    public interface ITransientOperation : IOperation
    {
    }

    public interface IScopedOperation : IOperation
    {
    }

    public interface ISingletonOperation : IOperation
    {
    }

    [TransientService]
    public class TransientOperation : ITransientOperation
    {
        public string OperationId { get; } = Guid.NewGuid().ToString()[^4..];
    }

    [ScopedService]
    public class ScopedOperation : IScopedOperation
    {
        public string OperationId { get; } = Guid.NewGuid().ToString()[^4..];
    }

    [SingletonService]
    public class SingletonOperation : ISingletonOperation
    {
        public string OperationId { get; } = Guid.NewGuid().ToString()[^4..];
    }

    public class OperationLogger
    {
        private readonly ITransientOperation _transientOperation;
        private readonly IScopedOperation _scopedOperation;
        private readonly ISingletonOperation _singletonOperation;

        public OperationLogger(
            ITransientOperation transientOperation,
            IScopedOperation scopedOperation,
            ISingletonOperation singletonOperation) =>
            (_transientOperation, _scopedOperation, _singletonOperation) =
                (transientOperation, scopedOperation, singletonOperation);

        public void LogOperations(string scope)
        {
            LogOperation(_transientOperation, scope, "Always different");
            LogOperation(_scopedOperation, scope, "Changes only with scope");
            LogOperation(_singletonOperation, scope, "Always the same");
        }


        private static void LogOperation<T>(T operation, string scope, string message)
            where T : IOperation =>
            Console.WriteLine(
                $"{scope}: {typeof(T).Name,-19} [ {operation.OperationId}...{message,-23} ]");
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class TransientServiceAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ScopedServiceAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class SingletonServiceAttribute : Attribute
    {
    }

    public static class ServiceCollectionServiceExtensions
    {
        public static IServiceCollection AddServicesFromAppDomain(this IServiceCollection services, Action<ServiceDiscoveryConfigurationBuilder> configurationBuilder = null) => AddServicesFromAssemblies(services, configurationBuilder, AppDomain.CurrentDomain.GetAssemblies());

        public static IServiceCollection AddServicesFromAssemblies(this IServiceCollection services, Action<ServiceDiscoveryConfigurationBuilder> configurationBuilder = null, params Assembly[] assemblies)
        {
            var configuration = new ServiceDiscoveryConfiguration();
            configurationBuilder?.Invoke(new ServiceDiscoveryConfigurationBuilder(configuration));

            foreach (var assembly in assemblies)
            {
                _AddServicesFromAssembly(services, assembly, configuration);
            }

            return services;
        }

        private static void _AddServicesFromAssembly(IServiceCollection services, Assembly assembly, ServiceDiscoveryConfiguration configuration)
        {
            var types = assembly.GetTypes();

            _AddServicesForLifetime(services, types, configuration.TransientServiceMarkerAttribute, ServiceLifetime.Transient);
            _AddServicesForLifetime(services, types, configuration.ScopedServiceMarkerAttribute, ServiceLifetime.Scoped);
            _AddServicesForLifetime(services, types, configuration.SingletonServiceMarkerAttribute, ServiceLifetime.Singleton);
        }

        private static void _AddServicesForLifetime(IServiceCollection services, Type[] types, Type serviceMarkerAttribute, ServiceLifetime serviceLifetime)
        {
            var transientServices = types.Where(t => t.GetCustomAttribute(serviceMarkerAttribute) != null);
            foreach (var transientService in transientServices)
            {
                var serviceInterfaces = transientService.GetInterfaces();
                foreach (var serviceInterface in serviceInterfaces)
                {
                    var descriptor = new ServiceDescriptor(serviceInterface, transientService, serviceLifetime);
                    services.Add(descriptor);
                }
            }
        }
    }

    internal class ServiceDiscoveryConfiguration
    {
        public Type TransientServiceMarkerAttribute = typeof(TransientServiceAttribute);
        public Type ScopedServiceMarkerAttribute = typeof(ScopedServiceAttribute);
        public Type SingletonServiceMarkerAttribute = typeof(SingletonServiceAttribute);
    }

    public class ServiceDiscoveryConfigurationBuilder
    {
        private readonly ServiceDiscoveryConfiguration _configuration;

        internal ServiceDiscoveryConfigurationBuilder(ServiceDiscoveryConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ServiceDiscoveryConfigurationBuilder UseTransientServiceMarkerAttribute<T>() => UseTransientServiceMarkerAttribute(typeof(T));

        public ServiceDiscoveryConfigurationBuilder UseTransientServiceMarkerAttribute(Type type)
        {
            _configuration.TransientServiceMarkerAttribute = type;
            return this;
        }

        public ServiceDiscoveryConfigurationBuilder UseScopedServiceMarkerAttribute<T>() => UseScopedServiceMarkerAttribute(typeof(T));

        public ServiceDiscoveryConfigurationBuilder UseScopedServiceMarkerAttribute(Type type)
        {
            _configuration.ScopedServiceMarkerAttribute = type;
            return this;
        }

        public ServiceDiscoveryConfigurationBuilder UseSingletonServiceMarkerAttribute<T>() => UseSingletonServiceMarkerAttribute(typeof(T));

        public ServiceDiscoveryConfigurationBuilder UseSingletonServiceMarkerAttribute(Type type)
        {
            _configuration.SingletonServiceMarkerAttribute = type;
            return this;
        }
    }
}
