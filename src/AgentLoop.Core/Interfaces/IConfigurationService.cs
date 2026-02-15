using AgentLoop.Data.Models;

namespace AgentLoop.Core.Interfaces;

public interface IConfigurationService
{
    AppSettings LoadSettings();
    Task SaveSettingsAsync(AppSettings settings);
    void UpdateStartupRegistration(bool enable);
}
