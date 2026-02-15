namespace AgentLoop.Core.Interfaces;

public interface IAppLogService
{
    void LogInfo(string action, string details);
    void LogError(Exception exception, string? context = null);
    void SetDebugMode(bool enabled);
    bool IsDebugEnabled { get; }
}
