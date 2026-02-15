using AgentLoop.Data.Models;
using IniParser;
using IniParser.Model;

namespace AgentLoop.Data.Services;

public class IniConfigurationService
{
    private readonly string _iniFilePath;
    private readonly FileIniDataParser _parser;

    public IniConfigurationService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgentLoop");
        Directory.CreateDirectory(appDataDir);
        _iniFilePath = Path.Combine(appDataDir, "agentloop.ini");
        _parser = new FileIniDataParser();
    }

    public IniConfigurationService(string iniFilePath)
    {
        _iniFilePath = iniFilePath;
        _parser = new FileIniDataParser();
        var dir = Path.GetDirectoryName(iniFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public AppSettings LoadSettings()
    {
        var data = LoadIniData();
        var section = data["Settings"];
        if (section == null)
            return new AppSettings();

        return new AppSettings
        {
            AgentCommand = section["agent_command"] ?? string.Empty,
            NotificationOnSuccess = ParseBool(section["notification_on_success"], true),
            NotificationOnFailure = ParseBool(section["notification_on_failure"], true),
            LogMaxSizeMb = ParseInt(section["log_max_size_mb"], 2),
            LogRetentionDays = ParseInt(section["log_retention_days"], 30),
            StartMinimizedToTray = ParseBool(section["start_minimized_to_tray"], false),
            StartWithWindows = ParseBool(section["start_with_windows"], true),
            DebugMode = ParseBool(section["debug_mode"], false),
            TaskFolder = section["task_folder"] ?? "AgentLoop",
            LogsDirectory = ResolveEnvVars(section["logs_directory"])
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AgentLoop", "logs"),
            ResultsDirectory = ResolveEnvVars(section["results_directory"])
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AgentLoop"),
        };
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var data = LoadIniData();
        EnsureSection(data, "Settings");
        var section = data["Settings"];

        section["agent_command"] = settings.AgentCommand;
        section["notification_on_success"] = settings.NotificationOnSuccess.ToString().ToLower();
        section["notification_on_failure"] = settings.NotificationOnFailure.ToString().ToLower();
        section["log_max_size_mb"] = settings.LogMaxSizeMb.ToString();
        section["log_retention_days"] = settings.LogRetentionDays.ToString();
        section["start_minimized_to_tray"] = settings.StartMinimizedToTray.ToString().ToLower();
        section["start_with_windows"] = settings.StartWithWindows.ToString().ToLower();
        section["debug_mode"] = settings.DebugMode.ToString().ToLower();
        section["task_folder"] = settings.TaskFolder;
        section["logs_directory"] = settings.LogsDirectory;
        section["results_directory"] = settings.ResultsDirectory;

        await Task.Run(() => SaveIniData(data));
    }

    private IniData LoadIniData()
    {
        if (!File.Exists(_iniFilePath))
            return new IniData();

        return _parser.ReadFile(_iniFilePath);
    }

    private void SaveIniData(IniData data)
    {
        var dir = Path.GetDirectoryName(_iniFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _parser.WriteFile(_iniFilePath, data);
    }

    private static void EnsureSection(IniData data, string sectionName)
    {
        if (!data.Sections.ContainsSection(sectionName))
            data.Sections.AddSection(sectionName);
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return value.Trim().ToLower() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return int.TryParse(value.Trim(), out var result) ? result : defaultValue;
    }

    private static string? ResolveEnvVars(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Environment.ExpandEnvironmentVariables(value);
    }
}
