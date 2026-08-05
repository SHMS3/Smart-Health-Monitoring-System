using Microsoft.Extensions.DependencyInjection;

namespace SmartHealthMonitoring.DI
{
    public static class DependencyInjectionSetup
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.Scan(scan => scan
                .FromAssemblyOf<Program>()

                .AddClasses(classes => classes.Where(type =>
                    type.Name.EndsWith("Service") || type.Name.EndsWith("Repository")))

                .AsImplementedInterfaces()

                .AsSelf()

                .WithScopedLifetime());


            services.AddSingleton<SmartHealthMonitoring.Interfaces.AI.IAiModelSessionRunner, SmartHealthMonitoring.Services.AI.AiModelSessionRunner>();

            return services;
        }
    }
}
