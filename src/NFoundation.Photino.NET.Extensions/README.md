# NFoundation.Photino.NET.Extensions

**A powerful extension library for Photino.NET that adds typed messaging, automatic script injection, and enhanced logging capabilities.**

## Overview

NFoundation.Photino.NET.Extensions enhances the Photino.NET framework with a comprehensive set of utilities for building desktop applications with web UI. It provides a fluent API for setting up typed communication between .NET and JavaScript, automatic script injection, and seamless console logging integration.

### Key Features

- 🚀 **Typed Messaging System** - Type-safe communication between .NET and JavaScript
- 📡 **Request-Response Patterns** - Async request handling with automatic response routing
- 📜 **Automatic Script Injection** - Embedded JavaScript library with auto-initialization
- 🪵 **Console Logging Bridge** - Forward JavaScript console messages to .NET ILogger
- 🔍 **Enhanced Photino Logging** - Routes Photino log messages to ILogger instance, rather than the Console (not supported for AOT compiled apps)

## Disclaimer

This project is an independent extension library for [Photino.NET](https://github.com/tryphotino/photino.NET).  
It is **not affiliated with, endorsed by, or sponsored by the Photino.NET maintainers**.  

Photino.NET is a separate open-source project licensed under the [Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0).

## Installation

Install the NuGet package:

```bash
dotnet add package NFoundation.Photino.NET.Extensions
```

Or via Package Manager Console:

```powershell
Install-Package NFoundation.Photino.NET.Extensions
```

### Prerequisites

- .NET 8.0 or later
- Photino.NET 3.2.3 or compatible version

## Quick Start

Here's a minimal example to get you started:

```csharp
using Microsoft.Extensions.Logging;
using Photino.NET;
using NFoundation.Photino.NET.Extensions;

// Set up logging
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("MainWindow");

// Initialize Photino log patcher (optional but recommended - does not work for AOT compiled apps)
PhotinoWindowLogPatcher.Initialize();

var window = new PhotinoWindow()
    .SetLogger(logger)
    .SetTitle("My App")
    .SetSize(new System.Drawing.Size(1200, 800))

    // Register message handlers
    .RegisterMessageHandler<string>("say-hello", (name) =>
    {
        logger.LogInformation("Hello from {Name}!", name);
    })

    // Register request handlers
    .RegisterRequestHandler<UserRequest, UserResponse>("get-user", async (request) =>
    {
        // Your async logic here
        return new UserResponse { Name = "John Doe" };
    })

    // Enable automatic script injection with console logging
    .RegisterPhotinoScript(enablePhotinoDebugLogging: true, forwardConsoleMessagesToLogger: true)

    // Load your HTML
    .Load("wwwroot/index.html");

window.WaitForClose();
```

In your HTML file:

```html
<!DOCTYPE html>
<html>
<head>
    <title>My App</title>
</head>
<body>
    <!-- Load the PhotinoWindow library -->
    <script src="photino://photinoWindow.js"></script>

    <script>
        // Send a one-way message
        function sayHello() {
            PhotinoWindow.sendMessage('say-hello', 'JavaScript');
        }

        // Send a request and handle the response
        async function getUser() {
            try {
                const user = await PhotinoWindow.sendRequest('get-user', { id: 1 });
                console.log('User:', user.name);
            } catch (error) {
                console.error('Error:', error.message);
            }
        }
    </script>
</body>
</html>
```

## Features in Detail

### Typed Messaging System

The library provides a robust typed messaging system for communication between .NET and JavaScript.

#### One-Way Messages

Perfect for fire-and-forget scenarios like button clicks, notifications, or status updates.

**.NET Side:**
```csharp
// Register a handler
window.RegisterMessageHandler<UserAction>("user-action", (action) =>
{
    logger.LogInformation("User {UserId} performed {ActionType}", action.UserId, action.Type);
});
```

**JavaScript Side:**
```javascript
// Send message
PhotinoWindow.sendMessage('user-action', {
    userId: 123,
    type: 'login',
    timestamp: new Date().toISOString()
});
```

#### Request-Response Pattern

Ideal for data fetching, form validation, or any scenario requiring a response.

**.NET Side:**
```csharp
// Register an async request handler
window.RegisterRequestHandler<GetUserRequest, UserResponse>("get-user", async (request) =>
{
    var user = await userService.GetUserAsync(request.UserId);
    return new UserResponse
    {
        Name = user.Name,
        Email = user.Email
    };
});

// Handle validation requests
window.RegisterRequestHandler<ValidateFormRequest, ValidationResult>("validate-form", async (request) =>
{
    var result = await formValidator.ValidateAsync(request);
    return result;
});
```

**JavaScript Side:**
```javascript
// Make a request and handle the response
async function loadUser(userId) {
    try {
        const user = await PhotinoWindow.sendRequest('get-user', { userId: userId });
        document.getElementById('userName').textContent = user.name;
        document.getElementById('userEmail').textContent = user.email;
    } catch (error) {
        console.error('Failed to load user:', error.message);
    }
}

// Validate a form with timeout
async function validateForm(formData) {
    try {
        const result = await PhotinoWindow.sendRequest('validate-form', formData, 5000); // 5s timeout
        if (result.isValid) {
            showSuccess('Form is valid!');
        } else {
            showErrors(result.errors);
        }
    } catch (error) {
        showError('Validation failed: ' + error.message);
    }
}
```

### JavaScript Integration

#### Automatic Script Injection

The library includes an embedded JavaScript client (`photinoWindow.js`) that's automatically served via a custom scheme handler.

**Configuration Options:**
- **`enablePhotinoDebugLogging`**: When true, enables debug logging from the Photino JavaScript framework itself in the browser console. This is useful for debugging message passing issues between JavaScript and .NET.
- **`forwardConsoleMessagesToLogger`**: When true (default), automatically forwards JavaScript console.log/warn/error messages to your .NET logger, allowing you to capture client-side logging in your server-side logs.

```csharp
// Basic setup with auto-initialization
window.RegisterPhotinoScript();

// Advanced setup with options
window.RegisterPhotinoScript(
    scheme: "photino",                           // Custom scheme name (default: "photino")
    enablePhotinoDebugLogging: true,             // Enable debug output for the Photino JavaScript framework (default: false)
    forwardConsoleMessagesToLogger: true         // Forward JS console messages to .NET logger (default: true)
);
```

**In your HTML:**
```html
<!-- The script auto-initializes with the options you specified in .NET -->
<script src="photino://photinoWindow.js"></script>
```

#### PhotinoWindow JavaScript API

Once loaded, the `PhotinoWindow` object provides a clean API:

```javascript
// Check initialization status
const stats = PhotinoWindow.getStatus()
console.log(`Initialized ${stats.initialized}, Handler Count: {stats.messageHandlers}, Pending Requests: {stats.pendingRequests}`);

// Send message
PhotinoWindow.sendMessage(type, payload);

// Send request with optional timeout
PhotinoWindow.sendRequest(type, payload, timeout = 30000);

// Register message handler (from .NET to JS)
PhotinoWindow.onMessage(type, handler);

// Remove message handler
PhotinoWindow.offMessage(type);

// Clear all handlers
PhotinoWindow.clearHandlers();

// Get statistics
const stats = PhotinoWindow.getStats();
console.log(`Handlers: ${stats.messageHandlers}, Pending: ${stats.pendingRequests}`);
```

### Console Logging Bridge

Forward JavaScript console output to your .NET logger for unified logging.

#### Setup

```csharp
// Enable console logging bridge (enabled by default)
window.RegisterPhotinoScript(forwardConsoleMessagesToLogger: true);
```

#### Usage

All JavaScript console methods are automatically forwarded:

```javascript
console.log('This appears in .NET logger as LogDebug');
console.info('This appears as LogInformation');
console.warn('This appears as LogWarning');
console.error('This appears as LogError');
console.debug('This appears as LogTrace');

// Complex objects are automatically serialized
console.log('User data:', { id: 1, name: 'John' });
```

**.NET Output:**
```
[12:34:56] DEBUG [JS Console] This appears in .NET logger as LogDebug
[12:34:56] INFO  [JS Console] This appears as LogInformation
[12:34:56] WARN  [JS Console] This appears as LogWarning
[12:34:56] ERROR [JS Console] This appears as LogError
[12:34:56] TRACE [JS Console] This appears as LogTrace
[12:34:56] DEBUG [JS Console] User data: {"id":1,"name":"John"}
```

## Advanced Features

### Enhanced Photino Logging

The library includes a Harmony-based patcher that intercepts Photino.NET's internal logging and routes it through your ILogger.

```csharp
// Initialize the log patcher at application startup
PhotinoWindowLogPatcher.Initialize();

// Now all Photino internal logs will use your configured logger
var window = new PhotinoWindow()
    .SetLogger(logger)  // This logger will receive both extension and Photino logs
    // ... rest of configuration
```

### Memory Management

The library uses `ConditionalWeakTable<PhotinoWindow, PhotinoWindowData>` for storing window-specific data, ensuring that:

- Window data is automatically garbage collected when windows are disposed
- No memory leaks from long-running applications
- Thread-safe access to window-specific configuration

### JSON Serialization Configuration

Configure JSON serialization for your message payloads. The library starts with sensible defaults from `JsonUtilities.GetSerializerOptions()` and allows you to customize them:

```csharp
// Optional
window.ConfigureJsonSerializerOptions(options =>
{
    // Default options are: camelCase, not indented
    // Included converters: JsonStringEnumConverter and JsonDateTimeConverter (microsecond precision)

    // Add JSON source generators for AOT/trimming support
    options.TypeInfoResolverChain.Add(MyJsonContext.Default);

    // Add custom converters
    options.Converters.Add(new JsonCustomConverter());
});
```

For AOT and trimming scenarios, create a JSON source generator context:

```csharp
[JsonSerializable(typeof(MyRequestType))]
[JsonSerializable(typeof(MyResponseType))]
internal partial class MyJsonContext : JsonSerializerContext
{
}
```

## API Reference

### Extension Methods

| Method | Description |
|--------|-------------|
| `SetLogger(ILogger)` | Configure logger for the window |
| `ConfigureJsonSerializerOptions(Action<JsonSerializerOptions>)` | Configure JSON serialization options |
| `RegisterMessageHandler<T>(string, Action<T>)` | Register one-way message handler |
| `UnregisterMessageHandler(string)` | Remove message handler |
| `RegisterRequestHandler<TReq, TRes>(string, Func<TReq, Task<TRes>>)` | Register async request handler |
| `UnregisterRequestHandler(string)` | Remove request handler |
| `SendMessage<T>(string, T)` | Send one-way message to JavaScript |
| `RegisterPhotinoScript(string, bool, bool)` | Enable script injection with options (scheme, enablePhotinoDebugLogging, forwardConsoleMessagesToLogger) |
| `ClearHandlers()` | Remove all registered handlers |

### Static Classes

| Class | Purpose |
|-------|---------|
| `PhotinoWindowLogPatcher` | Harmony-based logging integration |
| `PhotinoWindowExtensions` | Main extension methods |

## Example Project

The library includes a complete example application demonstrating all features:

**Location:** `NFoundation.Templates.Photino.NET.App`

**To run the example:**
```bash
cd src/NFoundation.Templates.Photino.NET.App
dotnet run
```

The example demonstrates:
- Typed messaging patterns
- Request-response handling
- Console logging bridge
- Error handling
- UI interactions

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Photino.NET | 3.2.3+ | Core desktop framework |
| Microsoft.Extensions.Logging | 8.0.0+ | Logging abstraction |
| NFoundation.Json | 1.0.0+ | JSON utilities |
| Lib.Harmony | 2.3.3+ | Runtime method patching |

## Requirements

- **.NET 8.0** or later
- **Windows, macOS, or Linux** (Photino.NET requirements)
- **Modern web browser engine** (embedded in application)

## License

This library is licensed under the [Apache License 2.0](LICENSE).

It depends on:
- [Photino.NET](https://github.com/tryphotino/photino.NET), which is also licensed under Apache License 2.0.
- [Harmony](https://github.com/pardeike/Harmony), which is also licensed under MIT License.  

See the [NOTICE](NOTICE) file for details.
