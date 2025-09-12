# NFoundation Console App Template

A .NET project template for creating console applications with pre-configured settings and structure.

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

- Pre-configured console application structure
- Multiple environment configuration files (Development, Windows, Linux)
- .NET 8.0 target framework
- Organized project layout

## Configuration Files

The template includes several appsettings files:
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development environment settings
- `appsettings.windows.json` - Windows-specific settings
- `appsettings.windows.Development.json` - Windows development settings
- `appsettings.linux.json` - Linux-specific settings

## Requirements

- .NET 8.0 SDK or later

## License

MIT

## Source

https://github.com/philrawlings/NFoundation