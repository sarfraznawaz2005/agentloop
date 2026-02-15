using AgentLoop.Core.Services;
using AgentLoop.Data.Models;
using Xunit;

namespace AgentLoop.Tests;

public class LogServiceTests : IDisposable
{
    private readonly string _tempLogsDir;
    private readonly LogService _service;

    public LogServiceTests()
    {
        _tempLogsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempLogsDir);
        _service = new LogService(_tempLogsDir);
    }

    [Fact]
    public async Task InsertAndGet_Works()
    {
        // Arrange
        var entry = new LogEntry
        {
            JobName = "TestJob",
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddMinutes(1),
            ExitCode = 0,
            Status = JobStatus.Success,
            Prompt = "Say Hello",
            LogFilePath = "log1.txt"
        };

        // Act
        await _service.InsertLogEntryAsync(entry);
        var retrieved = await _service.GetLogEntryByIdAsync(entry.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(entry.JobName, retrieved.JobName);
        Assert.Equal(entry.LogFilePath, retrieved.LogFilePath);
        Assert.Equal(entry.Status, retrieved.Status);
    }

    [Fact]
    public async Task GetJobLogs_ReturnsOnlyJobSpecificLogs()
    {
        // Arrange
        await _service.InsertLogEntryAsync(new LogEntry { JobName = "JobA", StartTime = DateTime.Now });
        await _service.InsertLogEntryAsync(new LogEntry { JobName = "JobB", StartTime = DateTime.Now });
        await _service.InsertLogEntryAsync(new LogEntry { JobName = "JobA", StartTime = DateTime.Now.AddSeconds(1) });

        // Act
        var jobALogs = await _service.GetJobLogsAsync("JobA");

        // Assert
        Assert.Equal(2, jobALogs.Count);
        Assert.All(jobALogs, l => Assert.Equal("JobA", l.JobName));
    }

    [Fact]
    public async Task SearchLogs_FindsMatchInPrompt()
    {
        // Arrange
        await _service.InsertLogEntryAsync(new LogEntry { JobName = "FindMe", Prompt = "Searching for needle", StartTime = DateTime.Now });
        await _service.InsertLogEntryAsync(new LogEntry { JobName = "Other", Prompt = "Just a haystack", StartTime = DateTime.Now });

        // Act
        var results = await _service.SearchLogsAsync("needle");

        // Assert
        Assert.Single(results);
        Assert.Equal("FindMe", results[0].JobName);
    }

    [Fact]
    public async Task PurgeOldLogs_RemovesOldEntries()
    {
        // Arrange
        var oldEntry = new LogEntry { JobName = "Old", StartTime = DateTime.Now.AddDays(-10) };
        var newEntry = new LogEntry { JobName = "New", StartTime = DateTime.Now };
        await _service.InsertLogEntryAsync(oldEntry);
        await _service.InsertLogEntryAsync(newEntry);

        // Act
        await _service.PurgeOldLogsAsync(5); // Retain 5 days
        var remaining = await _service.GetRecentLogsAsync();

        // Assert
        Assert.Single(remaining);
        Assert.Equal("New", remaining[0].JobName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempLogsDir))
        {
            try { Directory.Delete(_tempLogsDir, true); } catch { }
        }
    }
}
