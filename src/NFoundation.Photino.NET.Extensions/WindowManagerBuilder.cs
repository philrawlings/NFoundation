using Microsoft.Extensions.DependencyInjection;

namespace NFoundation.Photino.NET.Extensions
{
    public class WindowManagerBuilder
    {
        private readonly IServiceCollection _services;

        public WindowManagerBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public WindowManagerBuilder AddWindow<TWindow>() where TWindow : Window
        {
            _services.AddSingleton<TWindow>();
            return this;
        }

        public WindowManagerBuilder AddHostedWindow<TWindow>() where TWindow : Window
        {
            _services.AddSingleton<TWindow>();
            _services.AddHostedService<WindowHostedService<TWindow>>();
            return this;
        }
    }
}