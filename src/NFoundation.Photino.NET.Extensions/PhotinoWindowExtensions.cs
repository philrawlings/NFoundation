using Photino.NET;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NFoundation.Json;
using System.Reflection;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NFoundation.Photino.NET.Extensions
{
    public static class PhotinoWindowExtensions
    {
        private static readonly ConditionalWeakTable<PhotinoWindow, PhotinoWindowData> _windowData = new();

        private static readonly ConcurrentDictionary<string, HotReloadWatcherInfo> _globalWatchers = new();

        #region Message Envelope

        public class MessageEnvelope
        {
            public string Type { get; set; } = string.Empty;

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? RequestId { get; set; }

            public object? Payload { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Error { get; set; }

            [JsonIgnore]
            public bool IsRequest => !string.IsNullOrEmpty(RequestId);

            [JsonIgnore]
            public bool IsResponse => Type == "response";

            [JsonIgnore]
            public bool HasError => !string.IsNullOrEmpty(Error);
        }

        #endregion

        #region Hot Reload Infrastructure

        /// <summary>
        /// Information about a shared hot reload watcher
        /// </summary>
        private class HotReloadWatcherInfo : IDisposable
        {
            private readonly object _lock = new();
            private bool _disposed = false;

            public PhotinoHotReloadMonitor Monitor { get; }
            public HashSet<PhotinoWindow> SubscribedWindows { get; } = new();
            public string NormalizedPath { get; }
            public ILogger? Logger { get; }

            public HotReloadWatcherInfo(string normalizedPath, PhotinoHotReloadMonitor monitor, ILogger? logger = null)
            {
                NormalizedPath = normalizedPath;
                Monitor = monitor;
                Logger = logger;
            }

            public bool AddWindow(PhotinoWindow window)
            {
                lock (_lock)
                {
                    if (_disposed) return false;
                    return SubscribedWindows.Add(window);
                }
            }

            public bool RemoveWindow(PhotinoWindow window)
            {
                lock (_lock)
                {
                    if (_disposed) return false;
                    return SubscribedWindows.Remove(window);
                }
            }

            public int SubscriberCount
            {
                get
                {
                    lock (_lock)
                    {
                        return _disposed ? 0 : SubscribedWindows.Count;
                    }
                }
            }

            public void SendReloadToAllWindows()
            {
                PhotinoWindow[] windows;
                lock (_lock)
                {
                    if (_disposed) return;
                    windows = SubscribedWindows.ToArray();
                }

                foreach (var window in windows)
                {
                    try
                    {
                        window.Reload();
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning(ex, "Failed to reload window");
                    }
                }
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposed) return;
                    _disposed = true;

                    SubscribedWindows.Clear();
                    Monitor?.Dispose();
                    Logger?.LogDebug("Disposed hot reload watcher for path: {Path}", NormalizedPath);
                }
            }
        }

        #endregion

        #region Handler Storage

        private class PhotinoWindowData
        {
            private readonly object _lock = new();
            private bool _baseHandlerRegistered = false;

            public Dictionary<string, Delegate> MessageHandlers { get; } = new();
            public Dictionary<string, Delegate> RequestHandlers { get; } = new();
            private JsonSerializerOptions? _serializerOptions;
            public HashSet<string> HotReloadWatchPaths { get; } = new();

            ~PhotinoWindowData()
            {
                // Clean up hot reload subscriptions when the window data is finalized
                CleanupHotReloadSubscriptionsOnFinalize();
            }
            public JsonSerializerOptions SerializerOptions
            {
                get
                {
                    if (_serializerOptions == null)
                    {
                        _serializerOptions = JsonUtilities.GetSerializerOptions();
                        // Automatically add internal source generator context for framework types
                        _serializerOptions.TypeInfoResolverChain.Add(PhotinoInternalJsonContext.Default);
                    }
                    return _serializerOptions;
                }
                set => _serializerOptions = value;
            }
            public ILogger? Logger { get; set; }

            public bool EnsureBaseHandlerRegistered(PhotinoWindow window)
            {
                lock (_lock)
                {
                    if (_baseHandlerRegistered)
                        return false;

                    _baseHandlerRegistered = true;
                    return true;
                }
            }
        }

        #endregion

        #region Configuration

        public static PhotinoWindow ConfigureJsonSerializerOptions(this PhotinoWindow window, Action<JsonSerializerOptions> configure)
        {
            var data = _windowData.GetOrCreateValue(window);

            // Start with default options from JsonUtilities
            var options = JsonUtilities.GetSerializerOptions();

            // Automatically add internal source generator context for framework types
            options.TypeInfoResolverChain.Add(PhotinoInternalJsonContext.Default);

            // Apply user configuration
            configure(options);

            // Store the configured options
            data.SerializerOptions = options;
            return window;
        }

        public static PhotinoWindow SetLogger(this PhotinoWindow window, ILogger logger)
        {
            var data = _windowData.GetOrCreateValue(window);
            data.Logger = logger;
            return window;
        }

        #endregion

        #region Message Handler Registration

        public static PhotinoWindow RegisterMessageHandler<T>(this PhotinoWindow window, string type, Action<T> handler)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("Message type cannot be null or empty", nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var data = _windowData.GetOrCreateValue(window);

            lock (data.MessageHandlers)
            {
                if (data.MessageHandlers.ContainsKey(type))
                    throw new InvalidOperationException($"Message handler for type '{type}' is already registered");

                data.MessageHandlers[type] = handler;
            }

            if (data.EnsureBaseHandlerRegistered(window))
            {
                window.RegisterWebMessageReceivedHandler((sender, message) => OnWebMessageReceived(window, message));
                data.Logger?.LogDebug("Registered base web message handler for window");
            }

            data.Logger?.LogDebug("Registered message handler for type: {Type}", type);
            return window;
        }

        public static PhotinoWindow UnregisterMessageHandler(this PhotinoWindow window, string type)
        {
            if (_windowData.TryGetValue(window, out var data))
            {
                lock (data.MessageHandlers)
                {
                    if (data.MessageHandlers.Remove(type))
                    {
                        data.Logger?.LogDebug("Unregistered message handler for type: {Type}", type);
                    }
                }
            }
            return window;
        }

        #endregion

        #region Request Handler Registration

        public static PhotinoWindow RegisterRequestHandler<TRequest, TResponse>(
            this PhotinoWindow window,
            string type,
            Func<TRequest, Task<TResponse>> handler)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("Request type cannot be null or empty", nameof(type));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var data = _windowData.GetOrCreateValue(window);

            lock (data.RequestHandlers)
            {
                if (data.RequestHandlers.ContainsKey(type))
                    throw new InvalidOperationException($"Request handler for type '{type}' is already registered");

                data.RequestHandlers[type] = handler;
            }

            if (data.EnsureBaseHandlerRegistered(window))
            {
                window.RegisterWebMessageReceivedHandler((sender, message) => OnWebMessageReceived(window, message));
                data.Logger?.LogDebug("Registered base web message handler for window");
            }

            data.Logger?.LogDebug("Registered request handler for type: {Type}", type);
            return window;
        }

        public static PhotinoWindow UnregisterRequestHandler(this PhotinoWindow window, string type)
        {
            if (_windowData.TryGetValue(window, out var data))
            {
                lock (data.RequestHandlers)
                {
                    if (data.RequestHandlers.Remove(type))
                    {
                        data.Logger?.LogDebug("Unregistered request handler for type: {Type}", type);
                    }
                }
            }
            return window;
        }

        #endregion

        #region Message Sending

        public static void SendMessage<T>(this PhotinoWindow window, string type, T payload)
        {
            var data = _windowData.GetOrCreateValue(window);
            var options = data.SerializerOptions;

            var envelope = new MessageEnvelope
            {
                Type = type,
                Payload = payload
            };

            var json = JsonSerializer.Serialize(envelope, options);
            window.SendWebMessage(json);

            data.Logger?.LogDebug("Sent message of type: {Type}", type);
        }

        /// <summary>
        /// Triggers a page reload in the browser window
        /// </summary>
        /// <param name="window">The PhotinoWindow instance</param>
        public static void Reload(this PhotinoWindow window)
        {
            window.SendMessage<object?>("__reload", null);

            var data = _windowData.GetOrCreateValue(window);
            data.Logger?.LogDebug("Triggered manual reload");
        }

        #endregion

        #region Message Receiving

        private static async void OnWebMessageReceived(PhotinoWindow window, string message)
        {
            if (!_windowData.TryGetValue(window, out var data))
                return;

            var options = data.SerializerOptions;

            try
            {
                var envelope = JsonSerializer.Deserialize<MessageEnvelope>(message, options);
                if (envelope == null)
                {
                    data.Logger?.LogWarning("Received null envelope from web message");
                    return;
                }

                data.Logger?.LogDebug("Received {MessageType} of type: {Type}",
                    envelope.IsRequest ? "request" : "message", envelope.Type);

                if (envelope.IsResponse)
                {
                    // Responses are not expected in this direction (JS to .NET)
                    data.Logger?.LogWarning("Unexpected response message received: {RequestId}", envelope.RequestId);
                    return;
                }

                if (envelope.IsRequest)
                {
                    await HandleRequest(window, data, envelope, options);
                }
                else
                {
                    HandleMessage(data, envelope, options);
                }
            }
            catch (JsonException ex)
            {
                data.Logger?.LogError(ex, "Failed to deserialize web message: {Message}", message);
            }
            catch (Exception ex)
            {
                data.Logger?.LogError(ex, "Error processing web message");
            }
        }

        private static void HandleMessage(PhotinoWindowData data, MessageEnvelope envelope, JsonSerializerOptions options)
        {
            Delegate? handler;
            lock (data.MessageHandlers)
            {
                if (!data.MessageHandlers.TryGetValue(envelope.Type, out handler))
                {
                    data.Logger?.LogWarning("No message handler registered for type: {Type}", envelope.Type);
                    return;
                }
            }

            try
            {
                // Get the generic type from the handler
                var handlerType = handler.GetType();
                var genericArgs = handlerType.GetGenericArguments();
                if (genericArgs.Length == 0)
                {
                    // Non-generic Action
                    ((Action)handler)();
                }
                else
                {
                    // Action<T>
                    var payloadType = genericArgs[0];
                    object? typedPayload = null;

                    if (envelope.Payload != null)
                    {
                        // Convert JsonElement to the expected type
                        if (envelope.Payload is JsonElement jsonElement)
                        {
                            var json = jsonElement.GetRawText();
                            typedPayload = JsonSerializer.Deserialize(json, payloadType, options);
                        }
                        else
                        {
                            typedPayload = Convert.ChangeType(envelope.Payload, payloadType);
                        }
                    }

                    handler.DynamicInvoke(typedPayload);
                }

                data.Logger?.LogDebug("Successfully handled message of type: {Type}", envelope.Type);
            }
            catch (Exception ex)
            {
                data.Logger?.LogError(ex, "Error handling message of type: {Type}", envelope.Type);
            }
        }

        private static async Task HandleRequest(
            PhotinoWindow window,
            PhotinoWindowData data,
            MessageEnvelope envelope,
            JsonSerializerOptions options)
        {
            var response = new MessageEnvelope
            {
                Type = "response",
                RequestId = envelope.RequestId
            };

            try
            {
                Delegate? handler;
                lock (data.RequestHandlers)
                {
                    if (!data.RequestHandlers.TryGetValue(envelope.Type, out handler))
                    {
                        response.Error = $"No request handler registered for type: {envelope.Type}";
                        data.Logger?.LogWarning("No request handler registered for type: {Type}", envelope.Type);
                        SendResponse(window, response, options);
                        return;
                    }
                }

                // Get the generic types from the handler
                // Handler is Func<TRequest, Task<TResponse>>
                var handlerType = handler.GetType();
                var genericArgs = handlerType.GetGenericArguments();

                if (genericArgs.Length < 2)
                {
                    response.Error = "Invalid request handler signature";
                    SendResponse(window, response, options);
                    return;
                }

                var requestType = genericArgs[0]; // TRequest
                // genericArgs[1] is Task<TResponse>, so we need to get TResponse from it
                var taskType = genericArgs[1]; // Task<TResponse>
                var responseType = taskType.GetGenericArguments().Length > 0
                    ? taskType.GetGenericArguments()[0]  // TResponse
                    : typeof(object);

                // Deserialize the request payload
                object? typedRequest = null;
                if (envelope.Payload != null)
                {
                    if (envelope.Payload is JsonElement jsonElement)
                    {
                        var json = jsonElement.GetRawText();
                        typedRequest = JsonSerializer.Deserialize(json, requestType, options);
                    }
                    else
                    {
                        typedRequest = Convert.ChangeType(envelope.Payload, requestType);
                    }
                }

                // Invoke the handler
                var task = handler.DynamicInvoke(typedRequest) as Task;
                if (task != null)
                {
                    await task;

                    // Get the result more safely for AOT scenarios
                    var result = GetTaskResult(task, data.Logger);
                    response.Payload = result;

                    data.Logger?.LogDebug("Task result type: {ResultType}, value: {Result}",
                        result?.GetType()?.Name ?? "null", result);
                }

                data.Logger?.LogDebug("Successfully handled request of type: {Type}", envelope.Type);
            }
            catch (Exception ex)
            {
                data.Logger?.LogError(ex, "Error handling request of type: {Type}", envelope.Type);
                response.Error = ex.Message;
            }

            SendResponse(window, response, options);
        }

        private static void SendResponse(PhotinoWindow window, MessageEnvelope response, JsonSerializerOptions options)
        {
            // Debug logging for AOT troubleshooting
            if (_windowData.TryGetValue(window, out var data))
            {
                data.Logger?.LogDebug("Serializing response: RequestId={RequestId}, PayloadType={PayloadType}, HasError={HasError}",
                    response.RequestId, response.Payload?.GetType()?.Name ?? "null", response.HasError);
            }

            try
            {
                var json = JsonSerializer.Serialize(response, options);
                data?.Logger?.LogDebug("Serialized JSON: {Json}", json);
                window.SendWebMessage(json);
            }
            catch (Exception ex)
            {
                data?.Logger?.LogError(ex, "Failed to serialize response");
                // Send error response
                var errorResponse = new MessageEnvelope
                {
                    Type = "response",
                    RequestId = response.RequestId,
                    Error = "Serialization failed: " + ex.Message
                };
                var errorJson = JsonSerializer.Serialize(errorResponse, options);
                window.SendWebMessage(errorJson);
            }
        }

        /// <summary>
        /// Gets the result from a Task in an AOT-friendly way
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern",
            Justification = "Task.Result property access is preserved by the runtime")]
        private static object? GetTaskResult(Task task, ILogger? logger = null)
        {
            // For AOT scenarios, we need to be more careful about reflection
            var taskType = task.GetType();

            // Debug logging to understand the actual task type
            logger?.LogDebug("GetTaskResult: taskType={TaskType}, IsGenericType={IsGenericType}, GenericTypeDefinition={GenericTypeDef}",
                taskType.FullName,
                taskType.IsGenericType,
                taskType.IsGenericType ? taskType.GetGenericTypeDefinition().FullName : "N/A");

            // Handle different task types in AOT scenarios

            // 1. Try standard Task<T>.Result property first
            var standardResultProperty = taskType.GetProperty("Result");
            if (standardResultProperty != null && standardResultProperty.PropertyType != typeof(void))
            {
                var result = standardResultProperty.GetValue(task);
                logger?.LogDebug("GetTaskResult: Successfully extracted result of type {ResultType} via Result property",
                    result?.GetType()?.FullName ?? "null");
                return result;
            }

            // 2. Handle AOT AsyncStateMachineBox scenario
            if (taskType.FullName?.Contains("AsyncStateMachineBox") == true)
            {
                logger?.LogDebug("GetTaskResult: Detected AsyncStateMachineBox, attempting alternative extraction");
                logger?.LogDebug("GetTaskResult: Task status - IsCompleted: {IsCompleted}, IsFaulted: {IsFaulted}, IsCanceled: {IsCanceled}",
                    task.IsCompleted, task.IsFaulted, task.IsCanceled);

                // Get the expected result type from generic arguments first
                if (taskType.IsGenericType)
                {
                    var genericArgs = taskType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        var resultType = genericArgs[0]; // Should be UserDataResponse
                        logger?.LogDebug("GetTaskResult: Expected result type from generics: {ResultType}", resultType.FullName);

                        // Try casting to Task<T> and accessing Result
                        try
                        {
                            var taskGenericType = typeof(Task<>).MakeGenericType(resultType);
                            if (taskGenericType.IsAssignableFrom(taskType) || task is Task)
                            {
                                // Cast to Task<T> and get Result
                                var castedTask = Convert.ChangeType(task, taskGenericType);
                                var resultProperty = taskGenericType.GetProperty("Result");
                                if (resultProperty != null)
                                {
                                    var result = resultProperty.GetValue(castedTask);
                                    if (result != null)
                                    {
                                        logger?.LogDebug("GetTaskResult: Successfully extracted result via Task<T> cast: {ResultType}",
                                            result.GetType().FullName);
                                        return result;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogDebug("GetTaskResult: Task<T> cast failed: {Error}", ex.Message);
                        }

                        // Try using reflection to find the result field
                        if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                        {
                            var fields = taskType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            logger?.LogDebug("GetTaskResult: Found {FieldCount} fields to inspect", fields.Length);

                            foreach (var field in fields)
                            {
                                logger?.LogDebug("GetTaskResult: Checking field {FieldName} of type {FieldType}",
                                    field.Name, field.FieldType.FullName);

                                // Check if this field contains our result type
                                if (field.FieldType == resultType ||
                                    field.FieldType.IsAssignableFrom(resultType) ||
                                    field.Name.Contains("result", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        var fieldValue = field.GetValue(task);
                                        if (fieldValue != null && fieldValue.GetType() == resultType)
                                        {
                                            logger?.LogDebug("GetTaskResult: Found result in field {FieldName}: {ResultType}",
                                                field.Name, fieldValue.GetType().FullName);
                                            return fieldValue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger?.LogDebug("GetTaskResult: Failed to get field {FieldName}: {Error}",
                                            field.Name, ex.Message);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Fallback: check generic type definition for debugging
            if (taskType.IsGenericType)
            {
                var genericTypeDef = taskType.GetGenericTypeDefinition();
                logger?.LogDebug("GetTaskResult: Generic type check failed - genericTypeDef={GenericTypeDef}, expectedTaskType={ExpectedTaskType}",
                    genericTypeDef.FullName, typeof(Task<>).FullName);
            }

            // For non-generic Task, there's no result
            logger?.LogDebug("GetTaskResult: No result found, returning null");
            return null;
        }

        #endregion

        #region Console Logging Bridge

        /// <summary>
        /// Represents a console log message from JavaScript
        /// </summary>
        internal class ConsoleLogMessage
        {
            public string Level { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string Timestamp { get; set; } = string.Empty;
        }

        /// <summary>
        /// Setup console logging bridge for a window
        /// </summary>
        private static void SetupConsoleLogging(PhotinoWindow window)
        {
            window.RegisterMessageHandler<ConsoleLogMessage>("__console_log", (consoleMessage) =>
            {
                var data = _windowData.GetOrCreateValue(window);
                var logger = data.Logger;

                if (logger == null)
                    return;

                var message = $"[JS Console] {consoleMessage.Message}";

                switch (consoleMessage.Level.ToLower())
                {
                    case "error":
                        logger.LogError(message);
                        break;
                    case "warn":
                        logger.LogWarning(message);
                        break;
                    case "info":
                        logger.LogInformation(message);
                        break;
                    case "debug":
                        logger.LogTrace(message);
                        break;
                    case "log":
                    default:
                        logger.LogDebug(message);
                        break;
                }
            });
        }

        #endregion

        #region Script Injection

        private static string? _cachedPhotinoScript = null;

        /// <summary>
        /// Gets the embedded photinoWindow.js script content
        /// </summary>
        public static string GetPhotinoWindowScript()
        {
            if (_cachedPhotinoScript != null)
                return _cachedPhotinoScript;

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "NFoundation.Photino.NET.Extensions.photinoWindow.js";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Embedded resource '{resourceName}' not found");

                using (var reader = new StreamReader(stream))
                {
                    _cachedPhotinoScript = reader.ReadToEnd();
                }
            }

            return _cachedPhotinoScript;
        }

        /// <summary>
        /// Registers a custom scheme handler that serves the PhotinoWindow script with auto-initialization
        /// </summary>
        /// <param name="window">The PhotinoWindow instance</param>
        /// <param name="scheme">The custom scheme name (default: "photino")</param>
        /// <param name="enablePhotinoDebugLogging">Enable debug logging for the Photino JavaScript framework itself (default: false)</param>
        /// <param name="forwardConsoleMessagesToLogger">Forward JavaScript console messages to the .NET logger (default: true)</param>
        public static PhotinoWindow RegisterPhotinoScript(this PhotinoWindow window, string scheme = "photino", bool enablePhotinoDebugLogging = false, bool forwardConsoleMessagesToLogger = true)
        {
            // Set up console logging bridge if enabled
            if (forwardConsoleMessagesToLogger)
            {
                SetupConsoleLogging(window);
            }

            window.RegisterCustomSchemeHandler(scheme, (object sender, string schemeValue, string url, out string contentType) =>
            {
                if (url.ToLower() == $"{scheme}://photinowindow.js")
                {
                    contentType = "text/javascript";
                    var script = GetPhotinoWindowScript();

                    // Build the initialization options JSON
                    var initOptions = $"{{ enablePhotinoDebugLogging: {enablePhotinoDebugLogging.ToString().ToLower()}, forwardConsoleMessagesToLogger: {forwardConsoleMessagesToLogger.ToString().ToLower()} }}";

                    // Replace the placeholder with actual options
                    script = script.Replace(
                        "/* PHOTINO_INIT_OPTIONS_PLACEHOLDER */{}/* PHOTINO_INIT_OPTIONS_END */",
                        $"/* PHOTINO_INIT_OPTIONS_PLACEHOLDER */{initOptions}/* PHOTINO_INIT_OPTIONS_END */");

                    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(script));
                }

                contentType = "text/plain";
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"Resource not found: {url}"));
            });

            var data = _windowData.GetOrCreateValue(window);
            data.Logger?.LogDebug("Registered PhotinoWindow script scheme handler for scheme: {Scheme}", scheme);

            return window;
        }

        #endregion

        #region Hot Reload Extension Methods

        /// <summary>
        /// Configuration options for hot reload
        /// </summary>
        public class HotReloadOptions
        {
            public int DebounceDelay { get; set; } = 200;
            public string FileFilter { get; set; } = "*.*";
            public bool IncludeSubdirectories { get; set; } = true;
            public bool EnableOnlyInDebug { get; set; } = true;
        }

        /// <summary>
        /// Loads content with hot reload monitoring. Supports both local files and URLs.
        /// For local files, resolves to the source directory to ensure hot reload works properly.
        /// </summary>
        /// <param name="window">The PhotinoWindow instance</param>
        /// <param name="watchPath">The directory path to watch for changes (e.g., "wwwroot")</param>
        /// <param name="htmlPath">The HTML file path or URL to load</param>
        /// <returns>The PhotinoWindow instance for chaining</returns>
        public static PhotinoWindow Load(this PhotinoWindow window, string watchPath, string htmlPath)
        {
            return Load(window, watchPath, htmlPath, null);
        }

        /// <summary>
        /// Loads content with hot reload monitoring. Supports both local files and URLs.
        /// For local files, resolves to the source directory to ensure hot reload works properly.
        /// </summary>
        /// <param name="window">The PhotinoWindow instance</param>
        /// <param name="watchPath">The directory path to watch for changes (e.g., "wwwroot")</param>
        /// <param name="htmlPath">The HTML file path or URL to load</param>
        /// <param name="configureOptions">Optional configuration for hot reload behavior</param>
        /// <returns>The PhotinoWindow instance for chaining</returns>
        public static PhotinoWindow Load(this PhotinoWindow window, string watchPath, string htmlPath, Action<HotReloadOptions>? configureOptions)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (string.IsNullOrEmpty(watchPath)) throw new ArgumentException("Watch path cannot be null or empty", nameof(watchPath));
            if (string.IsNullOrEmpty(htmlPath)) throw new ArgumentException("HTML path cannot be null or empty", nameof(htmlPath));

            var data = _windowData.GetOrCreateValue(window);
            var logger = data.Logger;

            // Configure options
            var options = new HotReloadOptions();
            configureOptions?.Invoke(options);

            // Check if hot reload should be enabled
            if (!Debugger.IsAttached && options.EnableOnlyInDebug)
            {
                logger?.LogDebug("Hot reload disabled in release build");
                if (IsUrl(htmlPath))
                    window.Load(htmlPath);
                else
                    window.Load(watchPath + "/" + htmlPath);
                return window;
            }

            try
            {
                // Detect if htmlPath is a URL
                if (IsUrl(htmlPath))
                {
                    logger?.LogDebug("Loading URL with hot reload watching: {Url}, WatchPath: {WatchPath}", htmlPath, watchPath);

                    // For URLs, just load the URL directly but still watch the path
                    EnableHotReloadWatching(window, watchPath, options, logger);
                    window.Load(htmlPath);
                }
                else
                {
                    // For local files, resolve the path properly
                    var resolvedLoadPath = ResolveLoadPath(watchPath, htmlPath, logger);
                    logger?.LogDebug("Loading file with hot reload: {ResolvedPath}, WatchPath: {WatchPath}", resolvedLoadPath, watchPath);

                    EnableHotReloadWatching(window, watchPath, options, logger);
                    window.Load(resolvedLoadPath);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to enable hot reload, falling back to regular Load()");
                window.Load(htmlPath);
            }

            return window;
        }

        private static bool IsUrl(string path)
        {
            return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveLoadPath(string watchPath, string htmlPath, ILogger? logger)
        {
            // Use PhotinoHotReloadMonitor's path resolution logic to find the source directory
            var resolvedWatchPath = ResolveWatchPath(watchPath);
            var loadPath = Path.Combine(resolvedWatchPath, htmlPath);

            logger?.LogDebug("Resolved load path: {WatchPath} + {HtmlPath} = {LoadPath}",
                resolvedWatchPath, htmlPath, loadPath);

            return loadPath;
        }

        private static string ResolveWatchPath(string pathToWatch)
        {
            // This mirrors the logic from PhotinoHotReloadMonitor.ResolvePath()
            // If it's an absolute path and exists, use it directly
            if (Path.IsPathRooted(pathToWatch) && Directory.Exists(pathToWatch))
            {
                return Path.GetFullPath(pathToWatch);
            }

            // Find the project root (development scenario)
            var searchDir = Directory.GetCurrentDirectory();

            while (searchDir != null)
            {
                // Check if this directory has a .csproj file (indicates project root)
                if (Directory.GetFiles(searchDir, "*.csproj").Any())
                {
                    // Try standard path first
                    var projectPath = Path.Combine(searchDir, pathToWatch);
                    if (Directory.Exists(projectPath))
                    {
                        return Path.GetFullPath(projectPath);
                    }

                    // Try Resources subfolder (for embedded resources projects)
                    var resourcesPath = Path.Combine(searchDir, "Resources", pathToWatch);
                    if (Directory.Exists(resourcesPath))
                    {
                        return Path.GetFullPath(resourcesPath);
                    }
                }

                // Move up one directory
                var parent = Directory.GetParent(searchDir);
                if (parent == null || parent.FullName == searchDir)
                    break;
                searchDir = parent.FullName;
            }

            // Fallback: try relative to current directory
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), pathToWatch);
            if (Directory.Exists(currentDirPath))
            {
                return Path.GetFullPath(currentDirPath);
            }

            // Last resort - return normalized path
            return Path.GetFullPath(pathToWatch);
        }

        private static void EnableHotReloadWatching(PhotinoWindow window, string watchPath, HotReloadOptions options, ILogger? logger)
        {
            var normalizedPath = Path.GetFullPath(ResolveWatchPath(watchPath));
            var data = _windowData.GetOrCreateValue(window);

            // Track this watch path for cleanup
            data.HotReloadWatchPaths.Add(normalizedPath);

            // Get or create shared watcher
            var watcherInfo = _globalWatchers.GetOrAdd(normalizedPath, path =>
            {
                logger?.LogDebug("Creating new hot reload watcher for path: {Path}", path);

                var monitor = PhotinoHotReloadMonitor.Create(
                    path,
                    () => {
                        // When files change, reload all subscribed windows
                        if (_globalWatchers.TryGetValue(path, out var info))
                        {
                            logger?.LogDebug("Hot reload triggered for path: {Path}", path);
                            info.SendReloadToAllWindows();
                        }
                    },
                    fileFilter: options.FileFilter,
                    debounceDelay: options.DebounceDelay,
                    logger: logger
                );

                return new HotReloadWatcherInfo(path, monitor, logger);
            });

            // Subscribe this window to the watcher
            watcherInfo.AddWindow(window);
            logger?.LogDebug("Subscribed window to hot reload watcher. Total subscribers: {Count}", watcherInfo.SubscriberCount);
        }

        #endregion

        #region Internal Logger Access

        /// <summary>
        /// Internal method to get the logger for a window (used by PhotinoWindowLogPatcher)
        /// </summary>
        internal static ILogger? GetWindowLogger(PhotinoWindow window)
        {
            return _windowData.TryGetValue(window, out var data) ? data.Logger : null;
        }

        /// <summary>
        /// Internal method to check if a window has a logger (used by PhotinoWindowLogPatcher)
        /// </summary>
        internal static bool HasWindowLogger(PhotinoWindow window)
        {
            return _windowData.TryGetValue(window, out var data) && data.Logger != null;
        }

        #endregion

        #region Cleanup

        public static void ClearHandlers(this PhotinoWindow window)
        {
            if (_windowData.TryGetValue(window, out var data))
            {
                // Clean up hot reload subscriptions
                CleanupHotReloadSubscriptions(window, data);

                data.MessageHandlers.Clear();
                data.RequestHandlers.Clear();
                data.Logger?.LogDebug("Cleared all handlers for window");

                // The entry will be removed automatically when the window is garbage collected.
                _windowData.Remove(window);
            }
        }

        private static void CleanupHotReloadSubscriptions(PhotinoWindow window, PhotinoWindowData data)
        {
            if (data.HotReloadWatchPaths.Count == 0) return;

            foreach (var watchPath in data.HotReloadWatchPaths.ToArray())
            {
                if (_globalWatchers.TryGetValue(watchPath, out var watcherInfo))
                {
                    watcherInfo.RemoveWindow(window);
                    data.Logger?.LogDebug("Unsubscribed window from hot reload watcher: {Path}", watchPath);

                    // If no more subscribers, dispose the watcher
                    if (watcherInfo.SubscriberCount == 0)
                    {
                        if (_globalWatchers.TryRemove(watchPath, out var removedWatcher))
                        {
                            removedWatcher.Dispose();
                            data.Logger?.LogDebug("Disposed unused hot reload watcher: {Path}", watchPath);
                        }
                    }
                }
            }

            data.HotReloadWatchPaths.Clear();
        }

        private static void CleanupHotReloadSubscriptionsOnFinalize()
        {
            // Note: During finalization, we can't access the specific window instance
            // but we can clean up watchers that have no subscribers
            var watchersToRemove = new List<string>();

            foreach (var kvp in _globalWatchers)
            {
                var watcherInfo = kvp.Value;
                if (watcherInfo.SubscriberCount == 0)
                {
                    watchersToRemove.Add(kvp.Key);
                }
            }

            foreach (var path in watchersToRemove)
            {
                if (_globalWatchers.TryRemove(path, out var watcher))
                {
                    watcher.Dispose();
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Internal JSON source generator context for framework types
    /// </summary>
    [JsonSerializable(typeof(PhotinoWindowExtensions.MessageEnvelope))]
    [JsonSerializable(typeof(PhotinoWindowExtensions.ConsoleLogMessage))]
    internal partial class PhotinoInternalJsonContext : JsonSerializerContext
    {
    }
}