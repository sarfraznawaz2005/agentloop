@echo off
cls
echo Formatting code...
dotnet format AgentLoop.sln
if %ERRORLEVEL% NEQ 0 (
    echo Formatting failed.
    exit /b %ERRORLEVEL%
)

echo Running tests...
dotnet test src\AgentLoop.Tests\AgentLoop.Tests.csproj --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo Tests failed.
    exit /b %ERRORLEVEL%
)

echo All tests passed!
