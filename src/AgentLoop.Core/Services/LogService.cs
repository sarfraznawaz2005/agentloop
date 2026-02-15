using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AgentLoop.Core.Interfaces;
using AgentLoop.Data.Models;
using AgentLoop.Data.Services;
using Microsoft.Data.Sqlite;

namespace AgentLoop.Core.Services;

public partial class LogService : ILogService
{
    private readonly string _logsDirectory;
    private readonly LogDatabaseContext _dbContext;

    public LogService(string logsDirectory)
    {
        _logsDirectory = logsDirectory;
        _dbContext = new LogDatabaseContext(logsDirectory);
    }

    public async Task InsertLogEntryAsync(LogEntry entry)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Logs (
                JobName, StartTime, EndTime, ExitCode, Status, 
                Prompt, Command, StandardOutput, StandardError, 
                DurationSeconds, AgentName, LogFilePath
            ) VALUES (
                $jobName, $startTime, $endTime, $exitCode, $status,
                $prompt, $command, $stdout, $stderr,
                $duration, $agentName, $logPath
            );
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("$jobName", entry.JobName);
        cmd.Parameters.AddWithValue("$startTime", entry.StartTime.ToString("o"));
        cmd.Parameters.AddWithValue("$endTime", entry.EndTime.ToString("o"));
        cmd.Parameters.AddWithValue("$exitCode", entry.ExitCode);
        cmd.Parameters.AddWithValue("$status", (int)entry.Status);
        cmd.Parameters.AddWithValue("$prompt", entry.Prompt);
        cmd.Parameters.AddWithValue("$command", entry.Command);
        cmd.Parameters.AddWithValue("$stdout", entry.StandardOutput);
        cmd.Parameters.AddWithValue("$stderr", entry.StandardError);
        cmd.Parameters.AddWithValue("$duration", entry.DurationSeconds);
        cmd.Parameters.AddWithValue("$agentName", entry.AgentName);
        cmd.Parameters.AddWithValue("$logPath", entry.LogFilePath);

        var id = (long?)await cmd.ExecuteScalarAsync();
        if (id.HasValue) entry.Id = id.Value;
    }

    public async Task<LogEntry?> GetLogEntryByIdAsync(long id)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Logs WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapReaderToLogEntry(reader);
        }
        return null;
    }

    public async Task<LogEntry?> GetLogEntryAsync(string logFilePath)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Logs WHERE LogFilePath = $path LIMIT 1";
        cmd.Parameters.AddWithValue("$path", logFilePath);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapReaderToLogEntry(reader);
        }

        return null;
    }

    public async Task<List<LogEntry>> GetJobLogsAsync(string jobName)
    {
        var entries = new List<LogEntry>();
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Logs WHERE JobName = $jobName ORDER BY StartTime DESC, Id DESC";
        cmd.Parameters.AddWithValue("$jobName", jobName);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(MapReaderToLogEntry(reader));
        }
        return entries;
    }

    public async Task<List<LogEntry>> GetRecentLogsAsync(int count = 20, int offset = 0)
    {
        var entries = new List<LogEntry>();
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Logs ORDER BY StartTime DESC, Id DESC LIMIT $count OFFSET $offset";
        cmd.Parameters.AddWithValue("$count", count);
        cmd.Parameters.AddWithValue("$offset", offset);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(MapReaderToLogEntry(reader));
        }
        return entries;
    }

    public async Task<List<LogEntry>> SearchLogsAsync(string searchText, int count = 20)
    {
        var entries = new List<LogEntry>();
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM Logs 
            WHERE JobName LIKE $search 
               OR StandardOutput LIKE $search 
               OR Prompt LIKE $search 
            ORDER BY StartTime DESC, Id DESC 
            LIMIT $count";

        cmd.Parameters.AddWithValue("$search", $"%{searchText}%");
        cmd.Parameters.AddWithValue("$count", count);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(MapReaderToLogEntry(reader));
        }
        return entries;
    }

    public async Task<int> GetJobLogCountAsync(string jobName)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Logs WHERE JobName = $jobName";
        cmd.Parameters.AddWithValue("$jobName", jobName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> GetRecentLogCountAsync()
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Logs";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<string> ReadLogContentAsync(string logFilePath)
    {
        var entry = await GetLogEntryAsync(logFilePath);
        if (entry == null) return string.Empty;
        return FormatLogEntry(entry);
    }

    public async Task<string> ReadLogContentByIdAsync(long id)
    {
        var entry = await GetLogEntryByIdAsync(id);
        if (entry == null) return string.Empty;
        return FormatLogEntry(entry);
    }

    public async Task<string> ReadStdoutContentAsync(string logFilePath)
    {
        var entry = await GetLogEntryAsync(logFilePath);
        if (entry == null) return string.Empty;
        return entry.StandardOutput;
    }

    public async Task<string> ReadStdoutContentByIdAsync(long id)
    {
        var entry = await GetLogEntryByIdAsync(id);
        if (entry == null) return string.Empty;
        return entry.StandardOutput;
    }

    public async Task DeleteLogAsync(long id)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteLogAsync(string logFilePath)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs WHERE LogFilePath = $path";
        cmd.Parameters.AddWithValue("$path", logFilePath);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearJobLogsAsync(string jobName)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs WHERE JobName = $jobName";
        cmd.Parameters.AddWithValue("$jobName", jobName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearAllLogsAsync()
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RotateLogsAsync(int maxSizeMb = 2)
    {
        // SQL implementation doesn't strictly need file-based rotation, 
        // but we can implement a logic to delete logs exceeding a count or age.
        // For now, let's keep it empty or implement PurgeOldLogs
        await Task.CompletedTask;
    }

    public async Task PurgeOldLogsAsync(int retentionDays)
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs WHERE StartTime < $cutoff";
        cmd.Parameters.AddWithValue("$cutoff", DateTime.Now.AddDays(-retentionDays).ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<long> GetLastLogIdAsync()
    {
        using var connection = _dbContext.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT MAX(Id) FROM Logs";
        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value ? 0 : Convert.ToInt64(result);
    }


    private LogEntry MapReaderToLogEntry(SqliteDataReader reader)
    {
        return new LogEntry
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            JobName = reader.GetString(reader.GetOrdinal("JobName")),
            StartTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("StartTime"))),
            EndTime = reader.IsDBNull(reader.GetOrdinal("EndTime")) ? DateTime.MinValue : DateTime.Parse(reader.GetString(reader.GetOrdinal("EndTime"))),
            ExitCode = reader.GetInt32(reader.GetOrdinal("ExitCode")),
            Status = (JobStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            Prompt = reader.IsDBNull(reader.GetOrdinal("Prompt")) ? "" : reader.GetString(reader.GetOrdinal("Prompt")),
            Command = reader.IsDBNull(reader.GetOrdinal("Command")) ? "" : reader.GetString(reader.GetOrdinal("Command")),
            StandardOutput = reader.IsDBNull(reader.GetOrdinal("StandardOutput")) ? "" : reader.GetString(reader.GetOrdinal("StandardOutput")),
            StandardError = reader.IsDBNull(reader.GetOrdinal("StandardError")) ? "" : reader.GetString(reader.GetOrdinal("StandardError")),
            DurationSeconds = reader.IsDBNull(reader.GetOrdinal("DurationSeconds")) ? 0 : reader.GetDouble(reader.GetOrdinal("DurationSeconds")),
            AgentName = reader.IsDBNull(reader.GetOrdinal("AgentName")) ? "" : reader.GetString(reader.GetOrdinal("AgentName")),
            LogFilePath = reader.IsDBNull(reader.GetOrdinal("LogFilePath")) ? "" : reader.GetString(reader.GetOrdinal("LogFilePath"))
        };
    }

    private string FormatLogEntry(LogEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Job Run Started: {entry.StartTime:yyyy-MM-dd HH:mm:ss} ===");
        sb.AppendLine($"Job: {entry.JobName}");
        sb.AppendLine($"Prompt: {entry.Prompt}");
        sb.AppendLine($"Command: {entry.Command}");
        sb.AppendLine();
        sb.AppendLine("--- STDOUT ---");
        sb.AppendLine(entry.StandardOutput);
        sb.AppendLine();
        sb.AppendLine("--- STDERR ---");
        sb.AppendLine(entry.StandardError);
        sb.AppendLine();
        sb.AppendLine($"=== Job Run Completed: {entry.EndTime:yyyy-MM-dd HH:mm:ss} ===");
        sb.AppendLine($"Exit Code: {entry.ExitCode}");
        sb.AppendLine($"Duration: {entry.DurationSeconds:F1}s");
        sb.AppendLine($"Status: {entry.Status}");
        return sb.ToString();
    }

}
