# NFoundation Console App Template

A .NET project template for creating console applications with pre-configured logging, configuration management, and dependency injection.

## Installation

```bash
dotnet new install NFoundation.Templates.ConsoleApp
```

## Usage

Create a new console application using this template:

```bash
dotnet new nfconsole -n MyApp
```

## Features

- **Serilog Integration**: Pre-configured structured logging with Serilog
- **Advanced Configuration Management**: Hierarchical configuration with environment and platform-specific overrides
- **Dependency Injection**: Built-in DI container setup
- **Host Builder Pattern**: Uses .NET Generic Host for proper application lifecycle management
- .NET 8.0 target framework

## Logging with Serilog

The template includes Serilog for structured logging with:
- Console sink and File sink included (extensible)
- Configurable log levels via appsettings
- Enrichers for thread and process information
- Support for multiple sinks configuration

Example configuration in appsettings:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### Serilog MinimumLevel Overrides

The `Override` section within Serilog configuration allows you to set different log levels for specific namespaces. This is useful for:
- Reducing noise from framework components (Microsoft.*, System.*)
- Increasing verbosity for specific application components during debugging
- Fine-tuning log output for different parts of your application

Example with multiple overrides:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning",
        "MyApp.DataAccess": "Debug"
      }
    }
  }
}
```

## Configuration System

The template uses a sophisticated configuration loading system that supports:

### Configuration Files Hierarchy

Files are loaded in the following order (later files override earlier ones):
1. `appsettings.json` - Base configuration
2. `appsettings.{Platform}.json` - Platform-specific (Windows/Linux)
3. `appsettings.{Environment}.json` - Environment-specific
4. `appsettings.{Platform}.{Environment}.json` - Platform and environment-specific

This allows you to maintain different settings for:
- Different operating systems (Windows vs Linux)
- Different environments (Development, Staging, Production)
- Combinations of both (e.g., Windows Development vs Linux Production)

## Project Structure

```
MyApp/
├── Program.cs                              # Application entry point with host configuration
├── Extensions/
│   └── ConfigurationBuilderExtensions.cs   # Configuration loading extensions
├── appsettings.json                        # Base configuration
├── appsettings.Development.json            # Development environment settings
├── appsettings.windows.json                # Windows-specific settings
├── appsettings.windows.Development.json    # Windows development settings
└── appsettings.linux.json                  # Linux-specific settings
```

## Requirements

- .NET 8.0 SDK or later

## License

MIT

## Source

https://github.com/philrawlings/NFoundation