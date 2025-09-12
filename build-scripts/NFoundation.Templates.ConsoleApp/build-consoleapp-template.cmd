@echo off
REM Wrapper script to build NFoundation.Templates.ConsoleApp NuGet package

REM Check if PowerShell Core is available
where pwsh >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Using PowerShell Core ^(pwsh^)
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-consoleapp-template.ps1" %*
) else (
    REM Fall back to Windows PowerShell
    where powershell >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        echo Using Windows PowerShell
        powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-consoleapp-template.ps1" %*
    ) else (
        echo ERROR: PowerShell is not available on this system
        exit /b 1
    )
)

pause
exit /b %ERRORLEVEL%