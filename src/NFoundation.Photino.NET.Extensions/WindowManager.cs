using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace NFoundation.Photino.NET.Extensions
{
    public class WindowManager : IWindowManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Type, Window> _windows = new();

        public WindowManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Window GetWindow<T>() where T : Window
        {
            return _windows.GetOrAdd(typeof(T), _ =>
            {
                var newWindow = _serviceProvider.GetRequiredService<T>();
                return newWindow;
            });
        }
    }
}
