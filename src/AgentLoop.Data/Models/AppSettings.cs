namespace AgentLoop.Data.Models;

public class AppSettings
{
    // Agent Configuration
    public string AgentCommand { get; set; } = string.Empty;

    // Notification Settings
    public bool NotificationOnSuccess { get; set; } = true;
    public bool NotificationOnFailure { get; set; } = true;

    // Log Settings
    public int LogMaxSizeMb { get; set; } = 2;
    public int LogRetentionDays { get; set; } = 30;

    // Application Settings
    public bool StartMinimizedToTray { get; set; }
    public bool StartWithWindows { get; set; } = true;

    // Debug Settings
    public bool DebugMode { get; set; }

    // Task Scheduler Settings
    public string TaskFolder { get; set; } = "AgentLoop";
    public string LogsDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgentLoop", "logs");

    public string ResultsDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgentLoop");
}
