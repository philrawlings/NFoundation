using Microsoft.Extensions.Logging;
using Photino.NET;
using System.Reflection;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace NFoundation.Photino.NET.Extensions
{
    /// <summary>
    /// Patches PhotinoWindow's private static Log method to use ILogger or console output.
    /// Call Initialize() once at application startup to apply the patch.
    /// Uses the same logger storage as PhotinoWindowExtensions for consistency.
    /// </summary>
    public static class PhotinoWindowLogPatcher
    {
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

                _harmony = new Harmony(typeof(PhotinoWindowLogPatcher).Name);

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

            // Check if this window has a specific logger (using shared storage)
            var logger = PhotinoWindowExtensions.GetWindowLogger(__instance);
            if (logger != null)
            {
                // Use ILogger for this window
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
    }
}