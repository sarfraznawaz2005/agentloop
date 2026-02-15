using AgentLoop.Data.Models;
using AgentLoop.Data.Services;
using Xunit;

namespace AgentLoop.Tests;

public class IniConfigurationServiceTests : IDisposable
{
    private readonly string _tempIniPath;
    private readonly IniConfigurationService _service;

    public IniConfigurationServiceTests()
    {
        _tempIniPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ini");
        _service = new IniConfigurationService(_tempIniPath);
    }

    [Fact]
    public void LoadSettings_ReturnsDefaults_WhenFileNotExists()
    {
        // Arrange
        // File does not exist

        // Act
        var settings = _service.LoadSettings();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.AgentCommand);
        Assert.True(settings.NotificationOnSuccess);
        Assert.True(settings.NotificationOnFailure);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_Works()
    {
        // Arrange
        var settings = new AppSettings
        {
            AgentCommand = "my-command",
            NotificationOnSuccess = false,
            DebugMode = true,
            LogMaxSizeMb = 5
        };

        // Act
        await _service.SaveSettingsAsync(settings);
        var loaded = _service.LoadSettings();

        // Assert
        Assert.Equal("my-command", loaded.AgentCommand);
        Assert.False(loaded.NotificationOnSuccess);
        Assert.True(loaded.DebugMode);
        Assert.Equal(5, loaded.LogMaxSizeMb);
    }

    public void Dispose()
    {
        if (File.Exists(_tempIniPath)) File.Delete(_tempIniPath);
    }
}
