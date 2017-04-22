using Microsoft.Extensions.DependencyInjection;

namespace LD38
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGameService<TInterface, TImplementation>(this IServiceCollection services)
            where TInterface : class, IGameService
            where TImplementation : class, TInterface
        {
            services.AddSingleton<TInterface, TImplementation>();
            services.AddSingleton<IGameService>(provider => provider.GetService<TInterface>());

            return services;
        }

        public static IServiceCollection AddGameService<T>(this IServiceCollection services)
            where T : class, IGameService
        {
            services.AddSingleton<T>();
            services.AddSingleton<IGameService>(provider => provider.GetService<T>());

            return services;
        }
    }
}
