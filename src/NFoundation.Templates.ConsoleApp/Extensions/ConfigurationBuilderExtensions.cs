using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;

namespace NFoundation.Templates.ConsoleApp
{
    public static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddAppSettingsFiles(this IConfigurationBuilder configuration, IHostEnvironment environment)
        {
            configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                configuration
                    .AddJsonFile("appsettings.windows.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.windows.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                configuration
                    .AddJsonFile("appsettings.linux.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.linux.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            }
            else
            {
                throw new Exception("OS platform is not supported.");
            }

            return configuration;
        }
    }
}
