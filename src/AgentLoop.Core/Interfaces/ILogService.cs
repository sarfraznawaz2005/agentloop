using AgentLoop.Data.Models;

namespace AgentLoop.Core.Interfaces;

public interface ILogService
{
    Task InsertLogEntryAsync(LogEntry entry);
    Task<LogEntry?> GetLogEntryByIdAsync(long id);
    Task<LogEntry?> GetLogEntryAsync(string logFilePath);
    Task<List<LogEntry>> GetJobLogsAsync(string jobName);
    Task<List<LogEntry>> GetRecentLogsAsync(int count = 20, int offset = 0);
    Task<List<LogEntry>> SearchLogsAsync(string searchText, int count = 20);
    Task<int> GetJobLogCountAsync(string jobName);
    Task<int> GetRecentLogCountAsync();
    Task<string> ReadLogContentAsync(string logFilePath);
    Task<string> ReadLogContentByIdAsync(long id);
    Task<string> ReadStdoutContentAsync(string logFilePath);
    Task<string> ReadStdoutContentByIdAsync(long id);
    Task DeleteLogAsync(long id);
    Task DeleteLogAsync(string logFilePath);
    Task ClearJobLogsAsync(string jobName);
    Task ClearAllLogsAsync();
    Task RotateLogsAsync(int maxSizeMb = 2);
    Task PurgeOldLogsAsync(int retentionDays);
    Task<long> GetLastLogIdAsync();
}
