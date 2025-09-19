using Microsoft.Extensions.Logging;
using Photino.NET;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace NFoundation.Photino.NET.Extensions.Sample
{
    // Console window will be displayed when running to show logs
    // Change <OutputType> to "WinExe" in csproj to hide it (works on Linux/MacOS too)
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Configure logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            var logger = loggerFactory.CreateLogger<Program>();
            var windowLogger = loggerFactory.CreateLogger("MainWindow");

            // Initialize Photino log patcher (optional but recommended)
            try
            {
                PhotinoWindowLogPatcher.Initialize();
                logger.LogInformation("Photino log patcher initialized successfully");
            }
            catch
            {
                logger.LogWarning("Failed to initialize Photino log patcher (expected in AOT scenarios)");
            }

            // Window title declared here for visibility
            string windowTitle = "PhotinoWindow Demo with Typed Messaging";

            var window = new PhotinoWindow()
                .SetLogger(windowLogger)

                // Optional
                .ConfigureJsonSerializerOptions(options =>
                {
                    // Add JSON source generator context for AOT/trimming support
                    options.TypeInfoResolverChain.Add(MyJsonContext.Default);
                })

                .SetTitle(windowTitle)
                .SetUseOsDefaultSize(false)
                .SetSize(new Size(1200, 900))
                .Center()
                .SetResizable(true)
                .SetDevToolsEnabled(true)

                // Register one-way message handlers
                .RegisterMessageHandler<string>("button-clicked", (buttonId) =>
                {
                    logger.LogInformation("Button clicked: {ButtonId}", buttonId);
                })

                // Register request-response handlers
                .RegisterRequestHandler<UserDataRequest, UserDataResponse>("get-user", async (request) =>
                {
                    logger.LogInformation("Getting user data for ID: {UserId}", request.UserId);

                    await Task.Delay(200); // Simulate database lookup

                    // Mock user data
                    var users = new Dictionary<int, UserDataResponse>
                    {
                        { 1, new UserDataResponse { Name = "John Doe", Email = "john@example.com", Age = 30 } },
                        { 2, new UserDataResponse { Name = "Jane Smith", Email = "jane@example.com", Age = 25 } },
                        { 3, new UserDataResponse { Name = "Bob Johnson", Email = "bob@example.com", Age = 35 } }
                    };

                    if (users.TryGetValue(request.UserId, out var user))
                    {
                        return user;
                    }
                    else
                    {
                        throw new ArgumentException($"User with ID {request.UserId} not found");
                    }
                })

                // Register the PhotinoWindow script with console logging enabled
                // This allows pages to load the script via <script src="photino://photinoWindow.js"></script>
                .RegisterPhotinoScript(enableDebugLogging: true, enableConsoleLogging: true)

                // Load the HTML content
                .Load("wwwroot/index.html");

            logger.LogInformation("Starting PhotinoWindow application: {Title}", windowTitle);
            window.WaitForClose();
            logger.LogInformation("PhotinoWindow application closed");
        }
    }

    // Example data classes for typed messaging
    public class UserDataRequest
    {
        public int UserId { get; set; }
    }

    public class UserDataResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    // JSON source generator context for AOT/trimming support
    [JsonSerializable(typeof(UserDataRequest))]
    [JsonSerializable(typeof(UserDataResponse))]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "JSON source generator context ensures type safety")]
    internal partial class MyJsonContext : JsonSerializerContext
    {
    }
}