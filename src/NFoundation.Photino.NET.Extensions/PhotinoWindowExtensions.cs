using Photino.NET;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NFoundation.Json;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

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
            private JsonSerializerOptions? _serializerOptions;
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

    /// <summary>
    /// Internal JSON source generator context for framework types
    /// </summary>
    [JsonSerializable(typeof(PhotinoWindowExtensions.MessageEnvelope))]
    [JsonSerializable(typeof(PhotinoWindowExtensions.ConsoleLogMessage))]
    internal partial class PhotinoInternalJsonContext : JsonSerializerContext
    {
    }
}