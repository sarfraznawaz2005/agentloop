using System.ServiceProcess;
using System.Diagnostics.Eventing.Reader;
using AgentLoop.Core.Interfaces;
using AgentLoop.Data.Models;
using AgentLoop.Data.Services;
using Microsoft.Win32.TaskScheduler;
using Task = System.Threading.Tasks.Task;

namespace AgentLoop.Core.Services;

public class TaskSchedulerService : ITaskSchedulerService
{
    private readonly string _taskFolder;
    private string _agentCommand;
    private readonly string _logsDirectory;
    public TaskSchedulerService(string taskFolder, string agentCommand, string logsDirectory)
    {
        _taskFolder = taskFolder;
        _agentCommand = agentCommand;
        _logsDirectory = logsDirectory;
    }

    public bool IsServiceRunning()
    {
        try
        {
            using var sc = new ServiceController("Schedule");
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryStartServiceAsync()
    {
        try
        {
            using var sc = new ServiceController("Schedule");

            // Refresh to get current status
            sc.Refresh();

            if (sc.Status == ServiceControllerStatus.Running)
                return true;

            // Check if service can be started
            if (sc.Status == ServiceControllerStatus.Stopped ||
                sc.Status == ServiceControllerStatus.Paused)
            {
                sc.Start();

                // Wait for the service to start (with timeout)
                await Task.Run(() =>
                {
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                });

                return sc.Status == ServiceControllerStatus.Running;
            }

            return false;
        }
        catch (Exception)
        {
            // Service start requires administrator privileges
            // or the service might be disabled
            return false;
        }
    }

    public List<JobModel> GetAllJobs()
    {
        var jobs = new List<JobModel>();
        try
        {
            using var ts = new TaskService();
            var folder = GetOrCreateFolder(ts);
            var tasks = folder.GetTasks();

            foreach (var task in tasks)
            {
                var metadata = JobMetadataHelper.Decode(task.Definition.RegistrationInfo.Description);
                if (metadata == null)
                    continue;

                jobs.Add(new JobModel
                {
                    Name = task.Name,
                    Prompt = metadata.Value.Prompt,
                    Silent = metadata.Value.Silent,
                    Enabled = task.Enabled,
                    AgentOverride = metadata.Value.AgentOverride,
                    HexColor = metadata.Value.HexColor,
                    Icon = metadata.Value.Icon,
                    Schedule = ParseScheduleFromTriggers(task.Definition.Triggers),
                    LastRunTime = (task.LastRunTime > DateTime.MinValue && task.LastRunTime.Year > 2000) ? task.LastRunTime : null,
                    NextRunTime = (task.NextRunTime > DateTime.MinValue) ? task.NextRunTime : null,
                    IsRunning = task.State == TaskState.Running
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[TaskSchedulerService] GetAllJobs failed: {ex}");
        }
        return jobs;
    }

    public Task CreateJobAsync(JobModel job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.Name, nameof(job.Name));
        ArgumentNullException.ThrowIfNull(job.Schedule, nameof(job.Schedule));

        return Task.Run(() =>
        {
            using var ts = new TaskService();
            var td = ts.NewTask();
            if (td == null)
                throw new InvalidOperationException("Failed to create task definition.");

            td.RegistrationInfo.Description = JobMetadataHelper.Encode(job);
            td.Settings.StartWhenAvailable = true;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(1);

            AddTriggers(td, job.Schedule);
            AddAction(td, job);

            var folder = GetOrCreateFolder(ts);

            if (folder == null)
                throw new InvalidOperationException($"Failed to get or create Task Scheduler folder '{_taskFolder}'.");

            folder.RegisterTaskDefinition(job.Name, td);

            if (!job.Enabled)
            {
                var registeredTask = folder.GetTasks().FirstOrDefault(t => t.Name == job.Name);
                if (registeredTask != null)
                    registeredTask.Enabled = false;
            }
        });
    }

    public Task UpdateJobAsync(JobModel job)
    {
        return UpdateJobAsync(job.Name, job);
    }

    public Task UpdateJobAsync(string originalName, JobModel job)
    {
        return Task.Run(() =>
        {
            using var ts = new TaskService();
            var folder = GetOrCreateFolder(ts);
            var existingTask = folder.GetTasks().FirstOrDefault(t => t.Name == originalName);

            if (existingTask == null)
                throw new InvalidOperationException($"Task '{originalName}' not found.");

            var td = existingTask.Definition;
            td.RegistrationInfo.Description = JobMetadataHelper.Encode(job);
            td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(1);

            td.Triggers.Clear();
            AddTriggers(td, job.Schedule);

            td.Actions.Clear();
            AddAction(td, job);

            folder.RegisterTaskDefinition(job.Name, td);

            if (originalName != job.Name)
            {
                folder.DeleteTask(originalName, false);
                folder.RegisterTaskDefinition(job.Name, td);
            }

            var updatedTask = folder.GetTasks().FirstOrDefault(t => t.Name == job.Name);
            if (updatedTask != null)
                updatedTask.Enabled = job.Enabled;
        });
    }

    public Task DeleteJobAsync(string jobName)
    {
        return Task.Run(() =>
        {
            using var ts = new TaskService();
            var folder = GetOrCreateFolder(ts);
            folder.DeleteTask(jobName, false);
        });
    }

    public Task SetJobEnabledAsync(string jobName, bool enabled)
    {
        return Task.Run(() =>
        {
            using var ts = new TaskService();
            var folder = GetOrCreateFolder(ts);
            var task = folder.GetTasks().FirstOrDefault(t => t.Name == jobName);
            if (task != null)
                task.Enabled = enabled;
        });
    }

    public Task PauseAllJobsAsync()
    {
        return Task.Run(() =>
        {
            using var ts = new TaskService();
            var folder = GetOrCreateFolder(ts);
            foreach (var task in folder.GetTasks())
                task.Enabled = false;
        });
    }

    public Task ResumeAllJobsAsync()
    {
        return Task.Run(() =>
        {
            using var ts = new TaskService();
            var folder = GetOrCreateFolder(ts);
            foreach (var task in folder.GetTasks())
                task.Enabled = true;
        });
    }

    public void UpdateAgentCommand(string newCommand)
    {
        _agentCommand = newCommand;
    }

    public async Task UpdateAllJobsAsync()
    {
        var jobs = GetAllJobs();
        foreach (var job in jobs)
        {
            await UpdateJobAsync(job);
        }
    }

    public DateTime? GetNextRunTime(string jobName)
    {
        using var ts = new TaskService();
        var folder = GetOrCreateFolder(ts);
        var task = folder.GetTasks().FirstOrDefault(t => t.Name == jobName);
        if (task?.NextRunTime is DateTime dt && dt > DateTime.MinValue)
            return dt;
        return null;
    }

    public DateTime? GetLastRunTime(string jobName)
    {
        using var ts = new TaskService();
        var folder = GetOrCreateFolder(ts);
        var task = folder.GetTasks().FirstOrDefault(t => t.Name == jobName);
        // Windows Task Scheduler can return 1899-12-30 or 0001-01-01 for never run
        // user report of ~26 years ago suggests year 2000 might also be a default in some cases
        if (task?.LastRunTime is DateTime dt && dt > DateTime.MinValue && dt.Year > 2000)
            return dt;
        return null;
    }

    public bool IsHistoryEnabled()
    {
        try
        {
            var config = new EventLogConfiguration("Microsoft-Windows-TaskScheduler/Operational");
            return config.IsEnabled;
        }
        catch
        {
            return false;
        }
    }

    public string GetTaskFolder() => _taskFolder;

    private TaskFolder GetOrCreateFolder(TaskService ts)
    {
        try
        {
            var folder = ts.GetFolder($"\\{_taskFolder}");
            if (folder != null)
                return folder;
        }
        catch
        {
            // Folder doesn't exist, create it
        }

        try
        {
            var createdFolder = ts.RootFolder.CreateFolder(_taskFolder);
            if (createdFolder == null)
                throw new InvalidOperationException($"CreateFolder returned null for '{_taskFolder}'.");
            return createdFolder;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create Task Scheduler folder '{_taskFolder}': {ex.Message}", ex);
        }
    }

    private void AddAction(TaskDefinition td, JobModel job)
    {
        // Use the app itself as a job runner for proper structured logging
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "AgentLoop.exe");

        // Base64-encode command and prompt to safely handle multi-line prompts,
        // special characters, and quotes in Windows Task Scheduler arguments.
        // The CLI handler in App.xaml.cs decodes these at execution time.
        var commandToUse = !string.IsNullOrWhiteSpace(job.AgentOverride) ? job.AgentOverride : _agentCommand;
        var encodedCommand = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(commandToUse));
        var encodedPrompt = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(job.Prompt));

        var action = new ExecAction(
            exePath,
            $"--run-job \"{job.Name}\" {encodedCommand} {encodedPrompt} \"{_logsDirectory}\"",
            null);

        td.Actions.Add(action);
    }

    private static void AddTriggers(TaskDefinition td, ScheduleModel schedule)
    {
        switch (schedule.Type)
        {
            case ScheduleType.Minute:
                AddMinuteTrigger(td, schedule);
                break;
            case ScheduleType.Hourly:
                AddHourlyTrigger(td, schedule);
                break;
            case ScheduleType.Daily:
                AddDailyTrigger(td, schedule);
                break;
            case ScheduleType.DailyRecurring:
                AddDailyRecurringTrigger(td, schedule);
                break;
            case ScheduleType.Weekly:
                AddWeeklyTrigger(td, schedule);
                break;
            case ScheduleType.MonthlyDates:
                AddMonthlyTrigger(td, schedule);
                break;
        }
    }

    private static void AddMinuteTrigger(TaskDefinition td, ScheduleModel schedule)
    {
        var trigger = new DailyTrigger { DaysInterval = 1 };
        trigger.StartBoundary = DateTime.Today;
        trigger.Repetition.Interval = TimeSpan.FromMinutes(schedule.Interval);
        trigger.Repetition.Duration = TimeSpan.FromHours(24);
        td.Triggers.Add(trigger);
    }

    private static void AddHourlyTrigger(TaskDefinition td, ScheduleModel schedule)
    {
        if (schedule.HasTimeRestrictions && schedule.RestrictedDays.Count > 0)
        {
            // Create weekly trigger for specific days with repetition
            var days = ConvertToDaysOfTheWeek(schedule.RestrictedDays);
            var trigger = new WeeklyTrigger(days);
            trigger.StartBoundary = DateTime.Today + (schedule.TimeWindowStart ?? TimeSpan.Zero);
            trigger.Repetition.Interval = TimeSpan.FromHours(schedule.Interval);

            if (schedule.TimeWindowStart.HasValue && schedule.TimeWindowEnd.HasValue)
            {
                trigger.Repetition.Duration = schedule.TimeWindowEnd.Value - schedule.TimeWindowStart.Value;
            }
            else
            {
                trigger.Repetition.Duration = TimeSpan.FromHours(24);
            }

            td.Triggers.Add(trigger);
        }
        else
        {
            var trigger = new DailyTrigger { DaysInterval = 1 };
            trigger.StartBoundary = DateTime.Today;
            trigger.Repetition.Interval = TimeSpan.FromHours(schedule.Interval);
            trigger.Repetition.Duration = TimeSpan.FromHours(24);
            td.Triggers.Add(trigger);
        }
    }

    private static void AddDailyTrigger(TaskDefinition td, ScheduleModel schedule)
    {
        var trigger = new DailyTrigger { DaysInterval = 1 };
        trigger.StartBoundary = DateTime.Today + schedule.RunTime;
        td.Triggers.Add(trigger);
    }

    private static void AddDailyRecurringTrigger(TaskDefinition td, ScheduleModel schedule)
    {
        var startTime = schedule.StartTime ?? TimeSpan.Zero;

        if (schedule.HasTimeRestrictions && schedule.RestrictedDays.Count > 0)
        {
            var days = ConvertToDaysOfTheWeek(schedule.RestrictedDays);
            var trigger = new WeeklyTrigger(days);
            trigger.StartBoundary = DateTime.Today + startTime;
            trigger.Repetition.Interval = TimeSpan.FromHours(schedule.Interval);

            if (schedule.TimeWindowStart.HasValue && schedule.TimeWindowEnd.HasValue)
            {
                trigger.Repetition.Duration = schedule.TimeWindowEnd.Value - startTime;
            }
            else
            {
                trigger.Repetition.Duration = TimeSpan.FromHours(24);
            }

            td.Triggers.Add(trigger);
        }
        else
        {
            var trigger = new DailyTrigger { DaysInterval = 1 };
            trigger.StartBoundary = DateTime.Today + startTime;
            trigger.Repetition.Interval = TimeSpan.FromHours(schedule.Interval);
            trigger.Repetition.Duration = TimeSpan.FromHours(24);
            td.Triggers.Add(trigger);
        }
    }

    private static void AddWeeklyTrigger(TaskDefinition td, ScheduleModel schedule)
    {
        if (schedule.DaysOfWeek.Count == 0)
            return;

        var days = ConvertToDaysOfTheWeek(schedule.DaysOfWeek);
        var trigger = new WeeklyTrigger(days);
        trigger.StartBoundary = DateTime.Today + schedule.RunTime;
        td.Triggers.Add(trigger);
    }

    private static void AddMonthlyTrigger(TaskDefinition td, ScheduleModel schedule)
    {
        if (schedule.MonthDays.Count == 0)
            return;

        var regularDays = schedule.MonthDays.Where(d => d >= 1 && d <= 31).ToArray();
        var hasLastDay = schedule.MonthDays.Contains(32);

        if (regularDays.Length > 0)
        {
            var trigger = new MonthlyTrigger();
            trigger.DaysOfMonth = regularDays;
            trigger.MonthsOfYear = MonthsOfTheYear.AllMonths;
            trigger.StartBoundary = DateTime.Today + schedule.RunTime;
            td.Triggers.Add(trigger);
        }

        if (hasLastDay)
        {
            // Use MonthlyDOW trigger for last day (last occurrence of any day)
            var trigger = new MonthlyDOWTrigger();
            trigger.WeeksOfMonth = WhichWeek.LastWeek;
            trigger.DaysOfWeek = DaysOfTheWeek.AllDays;
            trigger.MonthsOfYear = MonthsOfTheYear.AllMonths;
            trigger.StartBoundary = DateTime.Today + schedule.RunTime;
            td.Triggers.Add(trigger);
        }
    }

    private static DaysOfTheWeek ConvertToDaysOfTheWeek(List<DayOfWeek> days)
    {
        var result = (DaysOfTheWeek)0;
        foreach (var day in days)
        {
            result |= day switch
            {
                DayOfWeek.Sunday => DaysOfTheWeek.Sunday,
                DayOfWeek.Monday => DaysOfTheWeek.Monday,
                DayOfWeek.Tuesday => DaysOfTheWeek.Tuesday,
                DayOfWeek.Wednesday => DaysOfTheWeek.Wednesday,
                DayOfWeek.Thursday => DaysOfTheWeek.Thursday,
                DayOfWeek.Friday => DaysOfTheWeek.Friday,
                DayOfWeek.Saturday => DaysOfTheWeek.Saturday,
                _ => 0
            };
        }
        return result;
    }

    private static ScheduleModel ParseScheduleFromTriggers(TriggerCollection triggers)
    {
        var model = new ScheduleModel();
        var trigger = triggers.FirstOrDefault();
        if (trigger == null) return model;

        switch (trigger)
        {
            case DailyTrigger dt:
                if (dt.Repetition.Interval > TimeSpan.Zero)
                {
                    if (dt.Repetition.Interval.TotalMinutes < 60)
                    {
                        model.Type = ScheduleType.Minute;
                        model.Interval = (int)dt.Repetition.Interval.TotalMinutes;
                    }
                    else
                    {
                        model.Type = ScheduleType.Hourly;
                        model.Interval = (int)dt.Repetition.Interval.TotalHours;
                    }
                }
                else
                {
                    model.Type = ScheduleType.Daily;
                    model.RunTime = dt.StartBoundary.TimeOfDay;
                }
                break;

            case WeeklyTrigger wt:
                if (wt.Repetition.Interval > TimeSpan.Zero)
                {
                    model.Type = ScheduleType.Hourly;
                    model.Interval = (int)wt.Repetition.Interval.TotalHours;
                    model.HasTimeRestrictions = true;
                    model.RestrictedDays = ParseDaysOfTheWeek(wt.DaysOfWeek);
                    model.TimeWindowStart = wt.StartBoundary.TimeOfDay;
                    if (wt.Repetition.Duration > TimeSpan.Zero)
                        model.TimeWindowEnd = wt.StartBoundary.TimeOfDay + wt.Repetition.Duration;
                }
                else
                {
                    model.Type = ScheduleType.Weekly;
                    model.DaysOfWeek = ParseDaysOfTheWeek(wt.DaysOfWeek);
                    model.RunTime = wt.StartBoundary.TimeOfDay;
                }
                break;

            case MonthlyTrigger mt:
                model.Type = ScheduleType.MonthlyDates;
                model.MonthDays = mt.DaysOfMonth.ToList();
                model.RunTime = mt.StartBoundary.TimeOfDay;
                break;

            case MonthlyDOWTrigger:
                model.Type = ScheduleType.MonthlyDates;
                model.MonthDays = [32]; // Last day
                model.RunTime = trigger.StartBoundary.TimeOfDay;
                break;
        }

        return model;
    }

    private static List<DayOfWeek> ParseDaysOfTheWeek(DaysOfTheWeek days)
    {
        var result = new List<DayOfWeek>();
        if (days.HasFlag(DaysOfTheWeek.Sunday)) result.Add(DayOfWeek.Sunday);
        if (days.HasFlag(DaysOfTheWeek.Monday)) result.Add(DayOfWeek.Monday);
        if (days.HasFlag(DaysOfTheWeek.Tuesday)) result.Add(DayOfWeek.Tuesday);
        if (days.HasFlag(DaysOfTheWeek.Wednesday)) result.Add(DayOfWeek.Wednesday);
        if (days.HasFlag(DaysOfTheWeek.Thursday)) result.Add(DayOfWeek.Thursday);
        if (days.HasFlag(DaysOfTheWeek.Friday)) result.Add(DayOfWeek.Friday);
        if (days.HasFlag(DaysOfTheWeek.Saturday)) result.Add(DayOfWeek.Saturday);
        return result;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
