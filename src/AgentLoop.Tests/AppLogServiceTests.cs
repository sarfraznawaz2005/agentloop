using AgentLoop.Core.Services;
using Xunit;

namespace AgentLoop.Tests;

public class AppLogServiceTests : IDisposable
{
    private readonly string _tempAppLog;
    private readonly string _tempErrorLog;
    private readonly AppLogService _service;

    public AppLogServiceTests()
    {
        _tempAppLog = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_app.log");
        _tempErrorLog = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_error.log");
        _service = new AppLogService(_tempAppLog, _tempErrorLog);
    }

    [Fact]
    public void LogInfo_DoesNotLogIfDebugDisabled()
    {
        // Arrange
        _service.SetDebugMode(false);

        // Act
        _service.LogInfo("Test", "Should not be logged");

        // Assert
        Assert.False(File.Exists(_tempAppLog));
    }

    [Fact]
    public void LogInfo_LogsIfDebugEnabled()
    {
        // Arrange
        _service.SetDebugMode(true);

        // Act
        _service.LogInfo("Test", "Should be logged");

        // Assert
        Assert.True(File.Exists(_tempAppLog));
        var content = File.ReadAllText(_tempAppLog);
        Assert.Contains("[INFO] [Test] Should be logged", content);
    }

    [Fact]
    public void LogError_WritesFullExceptionDetails()
    {
        // Arrange
        var ex = new InvalidOperationException("Test exception");

        // Act
        _service.LogError(ex, "Additional context");

        // Assert
        Assert.True(File.Exists(_tempErrorLog));
        var content = File.ReadAllText(_tempErrorLog);
        Assert.Contains("=== ERROR LOG ENTRY ===", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("Test exception", content);
        Assert.Contains("Additional Context: Additional context", content);
    }

    public void Dispose()
    {
        if (File.Exists(_tempAppLog)) File.Delete(_tempAppLog);
        if (File.Exists(_tempErrorLog)) File.Delete(_tempErrorLog);
    }
}
