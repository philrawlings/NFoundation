using Microsoft.Extensions.DependencyInjection;

namespace NFoundation.Photino.NET.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static WindowManagerBuilder AddWindowManager(this IServiceCollection services)
        {
            services.AddSingleton<IWindowManager, WindowManager>();
            return new WindowManagerBuilder(services);
        }
    }
}