using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NFoundation.Photino.NET.Extensions
{
    public class WindowHostedService<TWindow> : IHostedService where TWindow : Window
    {
        private readonly IWindowManager _windowManager;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<WindowHostedService<TWindow>> _logger;
        private Window? _window;
        private Task? _windowTask;

        public WindowHostedService(
            IWindowManager windowManager,
            IHostApplicationLifetime applicationLifetime,
            ILogger<WindowHostedService<TWindow>> logger)
        {
            _windowManager = windowManager;
            _applicationLifetime = applicationLifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting hosted window service for {WindowType}", typeof(TWindow).Name);

            _window = _windowManager.GetWindow<TWindow>();
            _window.Open();

            // Start monitoring the window closure asynchronously
            _windowTask = MonitorWindowAsync(_applicationLifetime.ApplicationStopping);

            return Task.CompletedTask;
        }

        private async Task MonitorWindowAsync(CancellationToken cancellationToken)
        {
            if (_window == null) return;

            try
            {
                // Wait for window to close
                await _window.WaitForCloseAsync(cancellationToken);

                // If window closed naturally (not due to cancellation), stop the application
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Window closed, stopping application");
                    _applicationLifetime.StopApplication();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while monitoring window");
                _applicationLifetime.StopApplication();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping hosted window service for {WindowType}", typeof(TWindow).Name);

            // Close the window if it's still open
            _window?.Close();

            // Wait for the monitoring task to complete
            if (_windowTask != null)
            {
                await _windowTask;
            }
        }
    }
}