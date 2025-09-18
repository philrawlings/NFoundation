using Microsoft.Extensions.Logging;
using Photino.NET;
using System.Reflection;
using System.Collections.Concurrent;
using HarmonyLib;

namespace NFoundation.Photino.NET.Extensions
{
    /// <summary>
    /// Patches PhotinoWindow's private static Log method to use ILogger or console output.
    /// Call Initialize() once at application startup to apply the patch.
    /// </summary>
    public static class PhotinoWindowLogPatcher
    {
        private static readonly ConcurrentDictionary<PhotinoWindow, ILogger> _windowLoggers = new();
        private static Harmony? _harmony;
        private static bool _isPatched = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initialize the patcher and apply Harmony patches.
        /// This should be called once at application startup.
        /// Windows without specific loggers will use Console.WriteLine.
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isPatched)
                {
                    return;
                }

                _harmony = new Harmony("com.windowwrappertest.photinowindowlogpatcher");

                // Find the private instance Log method in PhotinoWindow
                var photinoWindowType = typeof(PhotinoWindow);
                var logMethod = photinoWindowType.GetMethod("Log",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);

                if (logMethod != null)
                {
                    // Apply the prefix patch
                    var prefix = typeof(PhotinoWindowLogPatcher).GetMethod(nameof(LogPrefix),
                        BindingFlags.NonPublic | BindingFlags.Static);

                    _harmony.Patch(logMethod, new HarmonyMethod(prefix));
                    _isPatched = true;
                }
                else
                {
                    throw new Exception("ERROR: Could not find PhotinoWindow.Log method to patch");
                }
            }
        }

        /// <summary>
        /// Unpatch the PhotinoWindow.Log method and restore original behavior.
        /// </summary>
        public static void Unpatch()
        {
            lock (_lock)
            {
                if (!_isPatched || _harmony == null)
                    return;

                _harmony.UnpatchAll(_harmony.Id);
                _isPatched = false;
                _windowLoggers.Clear();
            }
        }

        /// <summary>
        /// Harmony prefix method that intercepts calls to PhotinoWindow.Log
        /// </summary>
        /// <returns>false to skip the original method, true to continue to it</returns>
        private static bool LogPrefix(PhotinoWindow __instance, string message)
        {
            // Get title value
            var titleProp = typeof(PhotinoWindow).GetProperty("Title",
                BindingFlags.Public | BindingFlags.Instance);
            var title = titleProp?.GetValue(__instance) as string ?? "PhotinoWindow";

            // Check if this window has a specific logger
            if (_windowLoggers.TryGetValue(__instance, out var logger))
            {
                // Use ILogger for this window
                if (message.Contains("error", StringComparison.OrdinalIgnoreCase))
                    logger.LogError("Photino {WindowTitle}{Message}", title, message);
                else if (message.Contains("warn", StringComparison.OrdinalIgnoreCase))
                    logger.LogWarning("Photino {WindowTitle}{Message}", title, message);
                else
                    logger.LogDebug("Photino {WindowTitle}{Message}", title, message);
            }
            else
            {
                // Fall back to Console.WriteLine
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logLevel = "DEBUG";

                if (message.Contains("error", StringComparison.OrdinalIgnoreCase))
                    logLevel = "ERROR";
                else if (message.Contains("warn", StringComparison.OrdinalIgnoreCase))
                    logLevel = "WARN ";

                Console.WriteLine($"[{timestamp}] {logLevel} Photino [{title}]: {message}");
            }

            // Return false to prevent the original Log method from executing
            return false;
        }


        /// <summary>
        /// Check if the patcher is currently active
        /// </summary>
        public static bool IsPatched => _isPatched;

        #region Extension Methods for PhotinoWindow

        /// <summary>
        /// Attach a specific ILogger to this PhotinoWindow instance
        /// </summary>
        public static PhotinoWindow SetLogger(this PhotinoWindow window, ILogger logger)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            if (!_isPatched)
            {
                throw new InvalidOperationException(
                    "PhotinoWindowLogPatcher must be initialized before setting window-specific loggers. " +
                    "Call PhotinoWindowLogPatcher.Initialize() first.");
            }

            _windowLoggers[window] = logger;
            logger.LogDebug("Logger attached to PhotinoWindow with title: {Title}", window.Title);

            return window;
        }

        /// <summary>
        /// Remove the specific ILogger from this PhotinoWindow instance (will fall back to default)
        /// </summary>
        public static PhotinoWindow RemoveLogger(this PhotinoWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            if (_windowLoggers.TryRemove(window, out var removedLogger))
            {
                removedLogger.LogDebug("Logger removed from PhotinoWindow with title: {Title}", window.Title);
            }

            return window;
        }

        /// <summary>
        /// Check if this PhotinoWindow has a specific logger attached
        /// </summary>
        public static bool HasLogger(this PhotinoWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            return _windowLoggers.ContainsKey(window);
        }

        /// <summary>
        /// Get the logger attached to this PhotinoWindow, or null if none
        /// </summary>
        public static ILogger? GetLogger(this PhotinoWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            return _windowLoggers.TryGetValue(window, out var logger) ? logger : null;
        }

        #endregion
    }
}