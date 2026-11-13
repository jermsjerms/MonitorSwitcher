@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK not found. Install it with:  winget install Microsoft.DotNet.SDK.8
  pause
  exit /b 1
)

echo Building MonitorSwitcher... (first build can take a minute)
dotnet publish "src\MonitorSwitcher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o "%~dp0_publish"
if errorlevel 1 (
  echo.
  echo Build failed. Copy the error text above and send it to Claude.
  pause
  exit /b 1
)

copy /y "_publish\MonitorSwitcher.exe" "MonitorSwitcher.exe" >nul
if errorlevel 1 (
  echo Could not write MonitorSwitcher.exe. Is the app still running? Close it and try again.
  pause
  exit /b 1
)

rem Clean up all build leftovers
rmdir /s /q "_publish" 2>nul
rmdir /s /q "src\bin" 2>nul
rmdir /s /q "src\obj" 2>nul

echo.
echo Done! MonitorSwitcher.exe is in this folder.
pause
