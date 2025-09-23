using Microsoft.Extensions.Logging;

namespace NFoundation.Photino.NET.Extensions
{
    /// <summary>
    /// Monitors source files for changes and triggers hot reload during development.
    /// This is specifically designed for development/debug scenarios and will attempt
    /// to find and watch the original source files, not the output/bin directory copies.
    /// </summary>
    public class PhotinoHotReloadMonitor : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Action _onChanged;
        private readonly ILogger? _logger;
        private Timer? _debounceTimer;
        private readonly int _debounceDelay;
        private readonly object _lock = new();
        private bool _disposed = false;

        /// <summary>
        /// Creates a new hot reload monitor for the specified path
        /// </summary>
        /// <param name="pathToWatch">The directory path to watch (e.g., "wwwroot")</param>
        /// <param name="onChanged">Callback to invoke when files change</param>
        /// <param name="filter">File filter pattern (e.g., "*.html", "*.*"). Default is "*.*"</param>
        /// <param name="includeSubdirectories">Whether to monitor subdirectories</param>
        /// <param name="debounceDelay">Delay in milliseconds before triggering callback after changes stop</param>
        /// <param name="logger">Optional logger for debugging</param>
        public PhotinoHotReloadMonitor(
            string pathToWatch,
            Action onChanged,
            string filter = "*.*",
            bool includeSubdirectories = true,
            int debounceDelay = 200,
            ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(pathToWatch))
                throw new ArgumentException("Path to watch cannot be null or empty", nameof(pathToWatch));

            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _debounceDelay = debounceDelay;
            _logger = logger;

            // Resolve the path
            var resolvedPath = ResolvePath(pathToWatch);

            if (!Directory.Exists(resolvedPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {resolvedPath}");
            }

            _logger?.LogDebug("Starting file watcher for: {Path}", resolvedPath);

            _watcher = new FileSystemWatcher(resolvedPath)
            {
                Filter = filter,
                NotifyFilter = NotifyFilters.LastWrite
                             | NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.Size,
                IncludeSubdirectories = includeSubdirectories,
                EnableRaisingEvents = true
            };

            // Subscribe to events
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Created += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnFileSystemEvent;
            _watcher.Error += OnWatcherError;
        }

        /// <summary>
        /// Creates a hot reload monitor for web development files
        /// </summary>
        public static PhotinoHotReloadMonitor Create(
            string pathToWatch,
            Action onChanged,
            string fileFilter,
            int debounceDelay = 200,
            ILogger? logger = null)
        {
            return new PhotinoHotReloadMonitor(
                pathToWatch,
                onChanged,
                fileFilter,
                true,
                debounceDelay,
                logger);
        }

        /// <summary>
        /// Resolves the path for watching, prioritizing development source folders over output directories
        /// </summary>
        private string ResolvePath(string pathToWatch)
        {
            // If it's an absolute path and exists, use it directly
            if (Path.IsPathRooted(pathToWatch) && Directory.Exists(pathToWatch))
            {
                return pathToWatch;
            }

            // FIRST: Try to find the project root (development scenario)
            // This is prioritized to ensure we watch source files, not bin/output copies
            var searchDir = Directory.GetCurrentDirectory();

            // Start from current directory and walk up to find project root
            while (searchDir != null)
            {
                // Check if this directory has a .csproj file (indicates project root)
                if (Directory.GetFiles(searchDir, "*.csproj").Any())
                {
                    // Try standard path first
                    var projectPath = Path.Combine(searchDir, pathToWatch);
                    if (Directory.Exists(projectPath))
                    {
                        _logger?.LogDebug("Hot reload monitoring source path: {Path}", projectPath);
                        return projectPath;
                    }

                    // Try Resources subfolder (for embedded resources projects)
                    var resourcesPath = Path.Combine(searchDir, "Resources", pathToWatch);
                    if (Directory.Exists(resourcesPath))
                    {
                        _logger?.LogDebug("Hot reload monitoring embedded resources path: {Path}", resourcesPath);
                        return resourcesPath;
                    }
                }

                // Move up one directory
                var parent = Directory.GetParent(searchDir);
                if (parent == null || parent.FullName == searchDir)
                    break;
                searchDir = parent.FullName;
            }

            // FALLBACK: Only use output directories if we can't find source
            // This might happen in unusual configurations

            // Try relative to current directory
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), pathToWatch);
            if (Directory.Exists(currentDirPath))
            {
                _logger?.LogWarning("Using output directory (not ideal for hot reload): {Path}", currentDirPath);
                return currentDirPath;
            }

            // Try relative to AppContext.BaseDirectory
            var appBasePath = Path.Combine(AppContext.BaseDirectory, pathToWatch);
            if (Directory.Exists(appBasePath))
            {
                _logger?.LogWarning("Using app base directory (not ideal for hot reload): {Path}", appBasePath);
                return appBasePath;
            }

            // Last resort - return the path as-is
            _logger?.LogWarning("Could not find source directory for hot reload, path may not exist: {Path}", pathToWatch);
            return pathToWatch;
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            // Filter out temporary files and directories
            if (ShouldIgnoreFile(e.FullPath))
            {
                return;
            }

            _logger?.LogDebug("File system event: {ChangeType} - {Path}", e.ChangeType, e.FullPath);

            lock (_lock)
            {
                // Cancel existing timer
                _debounceTimer?.Dispose();

                // Start new timer
                _debounceTimer = new Timer(_ =>
                {
                    try
                    {
                        _logger?.LogDebug("Triggering reload due to file changes");
                        _onChanged.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in file watcher callback");
                    }
                }, null, _debounceDelay, Timeout.Infinite);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger?.LogError(e.GetException(), "File watcher error");
        }

        private bool ShouldIgnoreFile(string path)
        {
            var fileName = Path.GetFileName(path);
            var extension = Path.GetExtension(path).ToLowerInvariant();

            // Ignore temporary files
            if (fileName.StartsWith(".") || fileName.StartsWith("~") || fileName.EndsWith("~"))
                return true;

            // Ignore common non-web files
            var ignoredExtensions = new HashSet<string>
            {
                ".tmp", ".temp", ".bak", ".swp", ".swo",
                ".log", ".cache", ".lock", ".DS_Store"
            };

            if (ignoredExtensions.Contains(extension))
                return true;

            return false;
        }

        /// <summary>
        /// Gets the resolved path being watched
        /// </summary>
        public string WatchPath => _watcher.Path;

        /// <summary>
        /// Gets whether the watcher is currently enabled
        /// </summary>
        public bool IsEnabled => _watcher.EnableRaisingEvents;

        /// <summary>
        /// Temporarily pause watching for changes
        /// </summary>
        public void Pause()
        {
            _watcher.EnableRaisingEvents = false;
            _logger?.LogDebug("File watcher paused");
        }

        /// <summary>
        /// Resume watching for changes
        /// </summary>
        public void Resume()
        {
            _watcher.EnableRaisingEvents = true;
            _logger?.LogDebug("File watcher resumed");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _disposed = true;
                _debounceTimer?.Dispose();
                _watcher?.Dispose();
                _logger?.LogDebug("File watcher disposed");
            }
        }
    }
}