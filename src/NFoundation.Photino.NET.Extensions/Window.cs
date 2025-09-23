using Microsoft.Extensions.Logging;
using Photino.NET;
using System.Runtime.InteropServices;

namespace NFoundation.Photino.NET.Extensions
{
    public abstract class Window : IDisposable
    {
        private ILogger _logger;
        private PhotinoWindow? _window = null;
        private Thread? _windowThread = null;
        private TaskCompletionSource? _closeCompletionSource = null;
        private bool _hasBeenOpened = false;

        protected Window(ILogger logger)
        {
            _logger = logger;
        }

        private bool disposedValue;

        protected abstract void Configure(PhotinoWindow window);

        public void Open(Window? parent = null)
        {
            if (_hasBeenOpened)
                throw new InvalidOperationException("This window has already been opened. Currently, windows may only be opened once.");

            if (_window is not null)
                throw new InvalidOperationException("Window is already open");

            _hasBeenOpened = true;
            _closeCompletionSource = new TaskCompletionSource();

            _windowThread = new Thread(() =>
            {
                try
                {
                    PhotinoWindowLogPatcher.Initialize();

                    _window = new PhotinoWindow(parent?._window)
                        .SetLogger(_logger)
                        .RegisterPhotinoScript();

                    Configure(_window);

                    _window.WaitForClose();
                    _closeCompletionSource?.TrySetResult();
                }
                catch (Exception ex)
                {
                    _closeCompletionSource?.TrySetException(ex);
                }
                finally
                {
                    _window = null;
                    _closeCompletionSource = null;
                }
            });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _windowThread.SetApartmentState(ApartmentState.STA); // Only supported on Windows
            _windowThread.Start();
        }

        public void Reload()
        {
            if (_window is null)
                throw new InvalidOperationException("Window is not open");

            _window?.Reload();
        }

        public void Close()
        {
            _window?.Close();
            _window = null; 
            _closeCompletionSource?.TrySetResult();
            _closeCompletionSource = null;
        }

        public Task WaitForCloseAsync(CancellationToken cancellationToken = default)
        {
            if (_closeCompletionSource == null)
                return Task.CompletedTask;

            if (cancellationToken != default)
                cancellationToken.Register(Close);

            return _closeCompletionSource.Task;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
