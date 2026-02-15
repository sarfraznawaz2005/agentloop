using Microsoft.Data.Sqlite;
using System.Data;

namespace AgentLoop.Data.Services;

public class LogDatabaseContext
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public LogDatabaseContext(string logsDirectory)
    {
        _databasePath = Path.Combine(logsDirectory, "agentloop_logs.db");
        // 'Default Timeout' is the correct keyword for Microsoft.Data.Sqlite
        _connectionString = $"Data Source={_databasePath};Default Timeout=5000;";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Enable WAL mode for performance and concurrent access
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            cmd.ExecuteNonQuery();
        }

        // Create Logs table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Logs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    JobName TEXT NOT NULL,
                    StartTime DATETIME NOT NULL,
                    EndTime DATETIME,
                    ExitCode INTEGER,
                    Status INTEGER,
                    Prompt TEXT,
                    Command TEXT,
                    StandardOutput TEXT,
                    StandardError TEXT,
                    DurationSeconds REAL,
                    AgentName TEXT,
                    LogFilePath TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_logs_jobname ON Logs(JobName);
                CREATE INDEX IF NOT EXISTS idx_logs_starttime ON Logs(StartTime DESC);
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
