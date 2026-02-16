namespace AgentLoop.Data.Models;

public class LogEntry
{
    public long Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public JobStatus Status { get; set; }
    public double DurationSeconds { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string LogFilePath { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}

public enum JobStatus
{
    Success,
    Failure
}
