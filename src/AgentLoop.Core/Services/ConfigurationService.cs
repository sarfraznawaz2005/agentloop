using AgentLoop.Core.Interfaces;
using AgentLoop.Data.Models;
using AgentLoop.Data.Services;
using Microsoft.Win32;
using System.Diagnostics;

namespace AgentLoop.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IniConfigurationService _iniService;

    public ConfigurationService()
    {
        _iniService = new IniConfigurationService();
    }

    public ConfigurationService(string iniFilePath)
    {
        _iniService = new IniConfigurationService(iniFilePath);
    }

    public AppSettings LoadSettings() => _iniService.LoadSettings();

    public Task SaveSettingsAsync(AppSettings settings) => _iniService.SaveSettingsAsync(settings);

    public void UpdateStartupRegistration(bool enable)
    {
        const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "AgentLoop";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch
        {
            // Fail silently if registry access is restricted
        }
    }
}
