@echo off
cls
set BUILD_DIR=dist

echo Cleaning previous builds...
if exist %BUILD_DIR% rd /s /q %BUILD_DIR%

echo Building production-ready AgentLoop.exe...
dotnet clean
dotnet build
dotnet publish src\AgentLoop.UI\AgentLoop.UI.csproj -c Release -r win-x64 --self-contained false -o %BUILD_DIR%

if %ERRORLEVEL% NEQ 0 (
    echo Build failed.
    exit /b %ERRORLEVEL%
)

echo Build successful! Build artifacts are in the '%BUILD_DIR%' directory.
dir %BUILD_DIR%
