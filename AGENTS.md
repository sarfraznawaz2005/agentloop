# AgentLoop - Agent Guidelines

## Build Commands

```bash
# Build entire solution
dotnet build AgentLoop.sln

# Build specific project
dotnet build src/AgentLoop.UI/AgentLoop.UI.csproj

# Run all tests
dotnet test src/AgentLoop.Tests/AgentLoop.Tests.csproj

# Run specific test class
dotnet test src/AgentLoop.Tests/AgentLoop.Tests.csproj --filter "FullyQualifiedName~AgentCommandServiceTests"

# Run single test method
dotnet test src/AgentLoop.Tests/AgentLoop.Tests.csproj --filter "FullyQualifiedName~SubstitutePrompt_ReplacesTokens"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Format code
dotnet format AgentLoop.sln

# Build production executable
build-win.bat  # Runs: dotnet publish as framework-dependent
```

## Project Structure

- **AgentLoop.Data**: Models (POCOs) and data services (INI config)
- **AgentLoop.Core**: Business logic, service implementations, interfaces
- **AgentLoop.UI**: WPF views, ViewModels, XAML, converters
- **AgentLoop.Tests**: xUnit tests with Moq

## Code Style Guidelines

### General
- **Language**: C# 12 with .NET 8 (Windows-only)
- **Framework**: WPF with MVVM pattern
- **Nullable**: Always enable (`<Nullable>enable</Nullable>`)
- **Implicit usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)

### Naming Conventions
- **Interfaces**: `IServiceName` (e.g., `ITaskSchedulerService`)
- **Services**: `ServiceName` implementing `IServiceName` (e.g., `TaskSchedulerService : ITaskSchedulerService`)
- **ViewModels**: `FeatureViewModel` inheriting from `ViewModelBase`
- **Models**: `ModelName` in `AgentLoop.Data.Models` namespace
- **Tests**: `ClassNameTests` with methods `MethodName_Scenario_ExpectedResult`
- **Private fields**: `_camelCase` with underscore prefix
- **Properties**: `PascalCase` with auto-initializers: `public string Name { get; set; } = string.Empty;`
- **Constants**: `PascalCase` for `static readonly`, `UPPER_SNAKE` for `const`

### Imports & Namespaces
```csharp
// System namespaces first
using System;
using System.Collections.Generic;

// Third-party
using Microsoft.Win32.TaskScheduler;

// Project namespaces (ordered by layer: Data -> Core -> UI)
using AgentLoop.Data.Models;
using AgentLoop.Core.Interfaces;
using AgentLoop.Core.Services;
```

Use file-scoped namespaces: `namespace AgentLoop.Core.Services;`

### Error Handling
- Prefer early returns over nested conditionals
- Use specific exceptions: `InvalidOperationException`, `ArgumentException`
- Include parameter name: `throw new ArgumentException("Message", nameof(param));`
- For async service methods, catch and return tuples: `(bool Success, string Error)`
- Avoid catching generic `Exception` unless logging and re-throwing

### MVVM Pattern
- **Models**: Plain POCOs in `AgentLoop.Data/Models/`, no UI dependencies
- **ViewModels**: All UI logic in `AgentLoop.UI/ViewModels/`, inherit from `ViewModelBase`
- **Views**: XAML only, minimal code-behind (only event wiring)
- Use `SetProperty()` helper for property change notifications
- Use `RelayCommand` or `AsyncRelayCommand` for commands

### Testing (xUnit + Moq)
```csharp
public class ServiceNameTests
{
    private readonly ServiceName _service;

    public ServiceNameTests()
    {
        _service = new ServiceName();
    }

    [Fact]
    public void MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        var input = "test";

        // Act
        var result = _service.Method(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("input", "expected")]
    public void MethodName_MultipleCases(string input, string expected) { }
}
```

### Key Principles
- DRY, KISS, SOLID
- Dependency injection via static properties in `App.xaml.cs`
- Services are singletons accessed via `App.ServiceName`
- Never skip requirements from PRD
- No TODO/FIXME comments - write complete working code
- Build after implementing to check for errors
- Ask clarifying questions before implementing unclear requirements
- If you are unsure about any requirement, behavior, or implementation detail, ask clarifying questions **before** writing code.
- At every step, provide a **high-level explanation** of what changes were made and why.
- After implementing changes or new features, always provide a list of **suggestions or improvements**, even if they differ from the user's original request.
- If the user requests a change or feature that is an **anti-pattern** or violates well-established best practices, clearly explain the issue and ask for confirmation before proceeding.
- Always use Context7 MCP when I need library/API documentation, code generation, setup or configuration steps without me having to explicitly ask.
- Always follow established and best practices in your implementations.
- Simplicity is key. If something can be done in easy way without complexity, prefer that.
