# NFoundation.Photino.NET.Extensions

**An extension library for Photino.NET that adds typed messaging, automatic script injection, and enhanced logging capabilities.**

## Overview

NFoundation.Photino.NET.Extensions enhances the Photino.NET framework with a comprehensive set of utilities for building desktop applications with web UI. It provides a fluent API for setting up typed communication between .NET and JavaScript, automatic script injection, and seamless console logging integration.

### Key Features

- 🚀 **Typed Messaging System** - Type-safe communication between .NET and JavaScript
- 📡 **Request-Response Patterns** - Async request handling with automatic response routing
- 📜 **Automatic Script Injection** - Embedded JavaScript library with auto-initialization
- 🪵 **Console Logging Bridge** - Forward JavaScript console messages to .NET ILogger
- 🔥 **Hot Reload for Development** - Automatic page refresh when source files change (DEBUG builds only)
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

    // Enable automatic script injection with console log messages bridged to host
    .RegisterPhotinoScript()

    // Load your HTML with hot reload support (automatically enabled in DEBUG builds)
    .Load("wwwroot", "index.html");

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
// Check status
const status = PhotinoWindow.getStatus()
console.log(`Initialized ${status.initialized}, Handler Count: ${status.messageHandlers}, Pending Requests: ${status.pendingRequests}`);

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
```

### Console Logging Bridge

Forward JavaScript console output to your .NET logger for unified logging.

#### Setup

```csharp
// Enable console logging bridge (enabled by default, therefore forwardConsoleMessagesToLogger can be omitted if preferred)
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

### Hot Reload for Development

The library provides automatic hot reload functionality that monitors your web files for changes and refreshes the application automatically during development.

#### Basic Usage

```csharp
var window = new PhotinoWindow()
    .SetLogger(logger)
    .SetTitle("My App")

    // Load with hot reload support - automatically enabled in DEBUG builds
    .Load("wwwroot", "index.html");

window.WaitForClose();
```

#### How It Works

- **Automatic Detection**: Hot reload is enabled automatically in DEBUG builds and disabled in RELEASE builds
- **Path Resolution**: Always loads from source directory (not bin/output) to ensure hot reload works properly
- **Multi-Window Support**: Multiple windows can watch the same directory efficiently using shared file watchers
- **URL Support**: Also works with development servers while still monitoring local files

#### Supported Scenarios

**Local Files:**
```csharp
// Watch wwwroot directory, load index.html from source
.Load("wwwroot", "index.html")

// Watch Resources/wwwroot, load admin.html
.Load("Resources/wwwroot", "admin.html")
```

**Development Servers:**
```csharp
// Watch wwwroot for file changes, but load from development server
.Load("wwwroot", "http://localhost:3000")

// Watch Resources/wwwroot, load from HTTPS development server
.Load("Resources/wwwroot", "https://localhost:5001")
```

#### Advanced Configuration

```csharp
// Custom hot reload configuration
.Load("wwwroot", "index.html", options =>
{
    options.DebounceDelay = 500;                    // Wait 500ms after changes stop
    options.FileFilter = "*.html,*.css,*.js";      // Only watch specific file types
    options.IncludeSubdirectories = true;           // Monitor subdirectories (default)
    options.EnableOnlyInDebug = false;              // Force enable in RELEASE builds
})
```

#### Path Resolution Logic

The hot reload system intelligently finds your source files:

1. **Project Root Detection**: Searches up the directory tree for `.csproj` files
2. **Source Priority**: Always prefers source directories over bin/output copies
3. **Embedded Resources**: Supports `Resources/wwwroot` pattern for embedded resource projects
4. **Fallback Handling**: Gracefully falls back to regular loading if source detection fails

#### Multi-Window Efficiency

When multiple windows watch the same directory:

```csharp
// Both windows share a single file watcher for "wwwroot"
var window1 = new PhotinoWindow().Load("wwwroot", "index.html");
var window2 = new PhotinoWindow().Load("wwwroot", "admin.html");

// Automatic cleanup when windows are disposed
window1.Dispose(); // Watcher continues for window2
window2.Dispose(); // Watcher is automatically disposed
```

#### Debugging Hot Reload

Enable debug logging to troubleshoot hot reload issues:

```csharp
var window = new PhotinoWindow()
    .SetLogger(logger) // Hot reload uses this logger for debug output
    .Load("wwwroot", "index.html");
```

**Debug Output:**
```
[12:34:56] DEBUG Hot reload monitoring source path: C:\MyProject\wwwroot
[12:34:57] INFO  Hot reload triggered for path: C:\MyProject\wwwroot
[12:34:57] DEBUG Sent hot reload message to window
```

#### JavaScript Integration

The hot reload system works seamlessly with the included JavaScript library. When files change, a `__reload` message is sent to all affected windows, triggering a page refresh:

```javascript
// This happens automatically - no JavaScript code needed
// But you can listen for the reload event if desired
PhotinoWindow.onMessage('__reload', () => {
    console.log('Page reload triggered');
    // Custom cleanup logic before reload if needed
});
```

#### Manual Reload

You can also trigger a reload manually from .NET:

```csharp
// Trigger a reload of the current page
window.Reload();
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
| `Reload()` | Trigger a page reload in the browser window |
| `Load(string, string)` | Load content with automatic hot reload support (watchPath, htmlPath) |
| `Load(string, string, Action<HotReloadOptions>?)` | Load content with configurable hot reload options |
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
