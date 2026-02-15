@echo off
cls && dotnet clean src/AgentLoop.UI/AgentLoop.UI.csproj && dotnet build src/AgentLoop.UI/AgentLoop.UI.csproj && dotnet run --project src/AgentLoop.UI/AgentLoop.UI.csproj