@echo off
echo Building AOT-optimized Photino.NET application for Windows...

cd /d "%~dp0..\NFoundation.Templates.Photino.NET.App"

echo.
echo Regular development build:
dotnet build
if %ERRORLEVEL% neq 0 goto :error

echo.
echo AOT publish for Windows (x64):
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true
if %ERRORLEVEL% neq 0 goto :error

set OUTPUT_PATH=%~dp0..\NFoundation.Templates.Photino.NET.App\bin\Release\net8.0\win-x64\publish\
echo.
echo ========================================
echo BUILD SUCCESSFUL!
echo ========================================
echo Output location: %OUTPUT_PATH%
echo Executable: %OUTPUT_PATH%NFoundation.Templates.Photino.NET.App.exe
echo.
dir "%OUTPUT_PATH%" /B
echo ========================================
pause
goto :end

:error
echo.
echo ========================================
echo BUILD FAILED!
echo ========================================
pause

:end