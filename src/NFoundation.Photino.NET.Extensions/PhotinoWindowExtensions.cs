using Photino.NET;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NFoundation.Json;
using System.Reflection;

namespace NFoundation.Photino.NET.Extensions
{
    public static class PhotinoWindowExtensions
    {
        private static readonly ConditionalWeakTable<PhotinoWindow, PhotinoWindowData> _windowData = new();

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

        #region Handler Storage

        private class PhotinoWindowData
        {
            private readonly object _lock = new();
            private bool _baseHandlerRegistered = false;

            public Dictionary<string, Delegate> MessageHandlers { get; } = new();
            public Dictionary<string, Delegate> RequestHandlers { get; } = new();
            public JsonSerializerOptions SerializerOptions { get; set; } = JsonUtilities.GetSerializerOptions();
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

        public static PhotinoWindow SetJsonSerializerOptions(this PhotinoWindow window, JsonSerializerOptions options)
        {
            var data = _windowData.GetOrCreateValue(window);
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

            data.Logger?.LogInformation("Registered message handler for type: {Type}", type);
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
                        data.Logger?.LogInformation("Unregistered message handler for type: {Type}", type);
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

            data.Logger?.LogInformation("Registered request handler for type: {Type}", type);
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
                        data.Logger?.LogInformation("Unregistered request handler for type: {Type}", type);
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

                    // Get the result using reflection
                    var resultProperty = task.GetType().GetProperty("Result");
                    response.Payload = resultProperty?.GetValue(task);
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
            var json = JsonSerializer.Serialize(response, options);
            window.SendWebMessage(json);
        }

        #endregion

        #region Console Logging Bridge

        /// <summary>
        /// Represents a console log message from JavaScript
        /// </summary>
        private class ConsoleLogMessage
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
        /// <param name="enableDebugLogging">Enable debug logging in PhotinoWindow (default: false)</param>
        /// <param name="enableConsoleLogging">Enable console logging bridge to .NET (default: true)</param>
        public static PhotinoWindow RegisterPhotinoScript(this PhotinoWindow window, string scheme = "photino", bool enableDebugLogging = false, bool enableConsoleLogging = true)
        {
            // Set up console logging bridge if enabled
            if (enableConsoleLogging)
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
                    var initOptions = $"{{ enableDebugLogging: {enableDebugLogging.ToString().ToLower()}, enableConsoleLogging: {enableConsoleLogging.ToString().ToLower()} }}";

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
            data.Logger?.LogInformation("Registered PhotinoWindow script scheme handler for scheme: {Scheme}", scheme);

            return window;
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
                data.MessageHandlers.Clear();
                data.RequestHandlers.Clear();
                data.Logger?.LogInformation("Cleared all handlers for window");

                // The entry will be removed automatically when the window is garbage collected.
                _windowData.Remove(window);
            }
        }

        #endregion
    }
}