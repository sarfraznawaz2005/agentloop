using AgentLoop.Core.Interfaces;

namespace AgentLoop.Core.Services;

public class AppLogService : IAppLogService
{
    private readonly string _appLogPath;
    private readonly string _errorLogPath;
    private readonly object _lock = new();

    public bool IsDebugEnabled { get; private set; }

    public AppLogService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgentLoop");
        Directory.CreateDirectory(appDataDir);
        _appLogPath = Path.Combine(appDataDir, "app.log");
        _errorLogPath = Path.Combine(appDataDir, "error.log");
    }

    public AppLogService(string appLogPath, string errorLogPath)
    {
        _appLogPath = appLogPath;
        _errorLogPath = errorLogPath;
    }

    public void SetDebugMode(bool enabled)
    {
        IsDebugEnabled = enabled;
    }

    public void LogInfo(string action, string details)
    {
        if (!IsDebugEnabled)
            return;

        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [{action}] {details}";
        WriteToFile(_appLogPath, entry);
    }

    public void LogError(Exception exception, string? context = null)
    {
        var entry = $"""
=== ERROR LOG ENTRY ===
Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Exception Type: {exception.GetType().FullName}
Message: {exception.Message}
Stack Trace: {exception.StackTrace}
Additional Context: {context ?? "None"}
========================
""";
        WriteToFile(_errorLogPath, entry);
    }

    private void WriteToFile(string path, string content)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(path, content + Environment.NewLine);
            }
            catch
            {
                // Swallow logging failures to prevent cascading errors
            }
        }
    }
}
