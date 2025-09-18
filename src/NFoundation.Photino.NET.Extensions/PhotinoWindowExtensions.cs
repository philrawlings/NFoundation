using Photino.NET;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NFoundation.Json;

namespace NFoundation.Photino.NET.Extensions
{
    public static class PhotinoWindowExtensions
    {
        private static readonly ConcurrentDictionary<PhotinoWindow, WindowHandlers> _windowHandlers = new();

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

        private class WindowHandlers
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
            var handlers = _windowHandlers.GetOrAdd(window, _ => new WindowHandlers());
            handlers.SerializerOptions = options;
            return window;
        }

        public static PhotinoWindow SetMessageLogger(this PhotinoWindow window, ILogger logger)
        {
            var handlers = _windowHandlers.GetOrAdd(window, _ => new WindowHandlers());
            handlers.Logger = logger;
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

            var handlers = _windowHandlers.GetOrAdd(window, _ => new WindowHandlers());

            lock (handlers.MessageHandlers)
            {
                if (handlers.MessageHandlers.ContainsKey(type))
                    throw new InvalidOperationException($"Message handler for type '{type}' is already registered");

                handlers.MessageHandlers[type] = handler;
            }

            if (handlers.EnsureBaseHandlerRegistered(window))
            {
                window.RegisterWebMessageReceivedHandler((sender, message) => OnWebMessageReceived(window, message));
                handlers.Logger?.LogDebug("Registered base web message handler for window");
            }

            handlers.Logger?.LogInformation("Registered message handler for type: {Type}", type);
            return window;
        }

        public static PhotinoWindow UnregisterMessageHandler(this PhotinoWindow window, string type)
        {
            if (_windowHandlers.TryGetValue(window, out var handlers))
            {
                lock (handlers.MessageHandlers)
                {
                    if (handlers.MessageHandlers.Remove(type))
                    {
                        handlers.Logger?.LogInformation("Unregistered message handler for type: {Type}", type);
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

            var handlers = _windowHandlers.GetOrAdd(window, _ => new WindowHandlers());

            lock (handlers.RequestHandlers)
            {
                if (handlers.RequestHandlers.ContainsKey(type))
                    throw new InvalidOperationException($"Request handler for type '{type}' is already registered");

                handlers.RequestHandlers[type] = handler;
            }

            if (handlers.EnsureBaseHandlerRegistered(window))
            {
                window.RegisterWebMessageReceivedHandler((sender, message) => OnWebMessageReceived(window, message));
                handlers.Logger?.LogDebug("Registered base web message handler for window");
            }

            handlers.Logger?.LogInformation("Registered request handler for type: {Type}", type);
            return window;
        }

        public static PhotinoWindow UnregisterRequestHandler(this PhotinoWindow window, string type)
        {
            if (_windowHandlers.TryGetValue(window, out var handlers))
            {
                lock (handlers.RequestHandlers)
                {
                    if (handlers.RequestHandlers.Remove(type))
                    {
                        handlers.Logger?.LogInformation("Unregistered request handler for type: {Type}", type);
                    }
                }
            }
            return window;
        }

        #endregion

        #region Message Sending

        public static void SendMessage<T>(this PhotinoWindow window, string type, T payload)
        {
            var handlers = _windowHandlers.GetOrAdd(window, _ => new WindowHandlers());
            var options = handlers.SerializerOptions;

            var envelope = new MessageEnvelope
            {
                Type = type,
                Payload = payload
            };

            var json = JsonSerializer.Serialize(envelope, options);
            window.SendWebMessage(json);

            handlers.Logger?.LogDebug("Sent message of type: {Type}", type);
        }


        #endregion

        #region Message Receiving

        private static async void OnWebMessageReceived(PhotinoWindow window, string message)
        {
            if (!_windowHandlers.TryGetValue(window, out var handlers))
                return;

            var options = handlers.SerializerOptions;

            try
            {
                var envelope = JsonSerializer.Deserialize<MessageEnvelope>(message, options);
                if (envelope == null)
                {
                    handlers.Logger?.LogWarning("Received null envelope from web message");
                    return;
                }

                handlers.Logger?.LogDebug("Received {MessageType} of type: {Type}",
                    envelope.IsRequest ? "request" : "message", envelope.Type);

                if (envelope.IsResponse)
                {
                    // Responses are not expected in this direction (JS to C#)
                    handlers.Logger?.LogWarning("Unexpected response message received: {RequestId}", envelope.RequestId);
                    return;
                }

                if (envelope.IsRequest)
                {
                    await HandleRequest(window, handlers, envelope, options);
                }
                else
                {
                    HandleMessage(handlers, envelope, options);
                }
            }
            catch (JsonException ex)
            {
                handlers.Logger?.LogError(ex, "Failed to deserialize web message: {Message}", message);
            }
            catch (Exception ex)
            {
                handlers.Logger?.LogError(ex, "Error processing web message");
            }
        }

        private static void HandleMessage(WindowHandlers handlers, MessageEnvelope envelope, JsonSerializerOptions options)
        {
            Delegate? handler;
            lock (handlers.MessageHandlers)
            {
                if (!handlers.MessageHandlers.TryGetValue(envelope.Type, out handler))
                {
                    handlers.Logger?.LogWarning("No message handler registered for type: {Type}", envelope.Type);
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

                handlers.Logger?.LogDebug("Successfully handled message of type: {Type}", envelope.Type);
            }
            catch (Exception ex)
            {
                handlers.Logger?.LogError(ex, "Error handling message of type: {Type}", envelope.Type);
            }
        }

        private static async Task HandleRequest(
            PhotinoWindow window,
            WindowHandlers handlers,
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
                lock (handlers.RequestHandlers)
                {
                    if (!handlers.RequestHandlers.TryGetValue(envelope.Type, out handler))
                    {
                        response.Error = $"No request handler registered for type: {envelope.Type}";
                        handlers.Logger?.LogWarning("No request handler registered for type: {Type}", envelope.Type);
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

                handlers.Logger?.LogDebug("Successfully handled request of type: {Type}", envelope.Type);
            }
            catch (Exception ex)
            {
                handlers.Logger?.LogError(ex, "Error handling request of type: {Type}", envelope.Type);
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

        #region Cleanup

        public static void ClearHandlers(this PhotinoWindow window)
        {
            if (_windowHandlers.TryRemove(window, out var handlers))
            {
                handlers.MessageHandlers.Clear();
                handlers.RequestHandlers.Clear();
                handlers.Logger?.LogInformation("Cleared all handlers for window");
            }
        }

        #endregion
    }
}