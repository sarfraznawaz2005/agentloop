using AgentLoop.Data.Models;

namespace AgentLoop.Core.Interfaces;

public interface ITaskSchedulerService
{
    bool IsServiceRunning();
    Task<bool> TryStartServiceAsync();
    List<JobModel> GetAllJobs();
    Task CreateJobAsync(JobModel job);
    Task UpdateJobAsync(JobModel job);
    Task UpdateJobAsync(string originalName, JobModel job);
    Task DeleteJobAsync(string jobName);
    Task SetJobEnabledAsync(string jobName, bool enabled);
    Task PauseAllJobsAsync();
    Task ResumeAllJobsAsync();
    Task UpdateAllJobsAsync();
    void UpdateAgentCommand(string newCommand);
    DateTime? GetNextRunTime(string jobName);
    DateTime? GetLastRunTime(string jobName);
    bool IsHistoryEnabled();
    string GetTaskFolder();
}
