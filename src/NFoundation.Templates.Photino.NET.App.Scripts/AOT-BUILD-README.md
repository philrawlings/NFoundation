# AOT Build Instructions

This project supports Ahead-of-Time (AOT) compilation for optimal performance and reduced startup time.

**Note**: These build scripts are located in `src/NFoundation.Templates.Photino.NET.App.Scripts/` and will build the project in the adjacent `NFoundation.Templates.Photino.NET.App` directory.

## Platform-Specific Build Scripts

Navigate to the scripts directory and use the appropriate build script for your target platform:

```bash
cd src/NFoundation.Templates.Photino.NET.App.Scripts/
```

### Windows
```cmd
build-aot.cmd
```
- Builds for: `win-x64`
- Output: `../NFoundation.Templates.Photino.NET.App/bin/Release/net8.0/win-x64/publish/NFoundation.Templates.Photino.NET.App.exe`

### Linux (x64)
```bash
./build-aot-linux-x64.sh
```
- Builds for: `linux-x64`
- Output: `../NFoundation.Templates.Photino.NET.App/bin/Release/net8.0/linux-x64/publish/NFoundation.Templates.Photino.NET.App`

### Linux (ARM64)
```bash
./build-aot-linux-arm64.sh
```
- Builds for: `linux-arm64` (Raspberry Pi 4+, ARM cloud instances)
- Output: `../NFoundation.Templates.Photino.NET.App/bin/Release/net8.0/linux-arm64/publish/NFoundation.Templates.Photino.NET.App`

### macOS (Intel)
```bash
./build-aot-macos-x64.sh
```
- Builds for: `osx-x64`
- Output: `../NFoundation.Templates.Photino.NET.App/bin/Release/net8.0/osx-x64/publish/NFoundation.Templates.Photino.NET.App`

### macOS (Apple Silicon)
```bash
./build-aot-macos-arm64.sh
```
- Builds for: `osx-arm64`
- Output: `../NFoundation.Templates.Photino.NET.App/bin/Release/net8.0/osx-arm64/publish/NFoundation.Templates.Photino.NET.App`

## Manual Build Commands

If you prefer to run the commands manually, navigate to the project directory first:

```bash
cd ../NFoundation.Templates.Photino.NET.App

# Development build
dotnet build

# AOT publish (replace {rid} with your target runtime identifier)
dotnet publish -c Release -r {rid} --self-contained -p:PublishAot=true
```

### Runtime Identifiers (RID)
- Windows: `win-x64`
- Linux x64: `linux-x64`
- Linux ARM64: `linux-arm64`
- macOS Intel: `osx-x64`
- macOS Apple Silicon: `osx-arm64`

## Output Structure

The published application will be a single, self-contained executable with all dependencies included. The output directory will contain:

- Main executable (no file extension on Unix platforms, `.exe` on Windows)
- Required native libraries
- Web assets (`wwwroot` folder)
- Configuration files

## AOT Limitations

- **Harmony Patching**: The PhotinoWindowLogPatcher may not work in AOT scenarios due to reflection limitations. The application will continue to function normally but may fall back to console logging.
- **JSON Serialization**: Uses source generators for optimal AOT compatibility.
- **Cross-compilation**: You must build on the target platform (e.g., build for macOS on a Mac).

## Performance Benefits

AOT compilation provides:
- **Faster startup** - No JIT compilation required
- **Smaller memory footprint** - Trimmed unused code
- **Predictable performance** - No JIT warm-up delays
- **Native execution** - Direct machine code execution