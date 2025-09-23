using Microsoft.Extensions.DependencyInjection;

namespace NFoundation.Photino.NET.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWindow<TWindow>(this IServiceCollection services) where TWindow : Window
        {
            services.AddSingleton<TWindow>();
            return services;
        }

        public static IServiceCollection AddHostedWindow<TWindow>(this IServiceCollection services) where TWindow : Window
        {
            services.AddSingleton<TWindow>();
            services.AddHostedService<WindowHostedService<TWindow>>();
            return services;
        }
    }
}