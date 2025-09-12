#!/usr/bin/env pwsh

# Build script for NFoundation.Templates.ConsoleApp NuGet package

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Script variables
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScriptsPath = Split-Path -Parent $scriptPath
$rootPath = Split-Path -Parent $buildScriptsPath
$srcPath = Join-Path $rootPath "src"
$templatePath = Join-Path $srcPath "NFoundation.Templates.ConsoleApp"
$nuspecFile = Join-Path $scriptPath "NFoundation.Templates.ConsoleApp.nuspec"
$outputPath = Join-Path $rootPath "builds"

# Read version from nuspec file
if (!(Test-Path $nuspecFile)) {
    Write-Error "NuSpec file not found: $nuspecFile"
    exit 1
}

[xml]$nuspecXml = Get-Content $nuspecFile
$Version = $nuspecXml.package.metadata.version

if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Error "Could not read version from nuspec file"
    exit 1
}

Write-Host "Building NFoundation.Templates.ConsoleApp NuGet package..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Version: $Version (from nuspec)" -ForegroundColor Cyan

# Ensure output directory exists
if (!(Test-Path $outputPath)) {
    Write-Host "Creating output directory: $outputPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

# Check if template directory exists
if (!(Test-Path $templatePath)) {
    Write-Error "Template directory not found: $templatePath"
    exit 1
}

# Pack the template using dotnet pack
Write-Host "Packing template using dotnet pack..." -ForegroundColor Yellow

# Execute dotnet pack from the script directory
Push-Location $scriptPath
try {
    $packResult = & dotnet pack --no-build --no-restore -p:PackageVersion=$Version -p:NuspecFile="NFoundation.Templates.ConsoleApp.nuspec" -o $outputPath 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "dotnet pack output:" -ForegroundColor Red
        Write-Host $packResult
        Write-Error "dotnet pack failed with exit code $LASTEXITCODE"
        exit 1
    }
    
    Write-Host $packResult
    
    # Check if package was created
    $packageFile = Join-Path $outputPath "NFoundation.Templates.ConsoleApp.$Version.nupkg"
    if (Test-Path $packageFile) {
        Write-Host "Package created successfully: $packageFile" -ForegroundColor Green
        Write-Host "Package size: $((Get-Item $packageFile).Length / 1KB) KB" -ForegroundColor Cyan
    } else {
        Write-Error "Package file not found after build: $packageFile"
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Host "Build completed successfully!" -ForegroundColor Green