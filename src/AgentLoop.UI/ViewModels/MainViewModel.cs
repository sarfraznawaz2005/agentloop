using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;
using AgentLoop.Core.Helpers;
using AgentLoop.UI.Views;
using AgentLoop.Core.Interfaces;
using System.Threading;

namespace AgentLoop.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private string _jobFilter = string.Empty;
    private string _logSearchText = string.Empty;
    private JobModel? _selectedJob;
    private bool _isTaskSchedulerRunning;
    private bool _isPaused;
    private int _recentLogsPageSize = 10;
    private int _recentLogsLoaded = 10;
    private bool _isUpcomingRunsExpanded;
    private bool _isRunLogsExpanded = true;
    private readonly DispatcherTimer _agoUpdateTimer;
    private readonly DispatcherTimer _dataRefreshTimer;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _isRefreshQueued;

    public ObservableCollection<JobModel> Jobs { get; } = [];
    public ObservableCollection<JobModel> FilteredJobs { get; } = [];
    public ObservableCollection<UpcomingRun> UpcomingRuns { get; } = [];
    public ObservableCollection<LogEntry> RecentLogs { get; } = [];

    public int RecentLogsLoadedCount => RecentLogs.Count;
    public bool HasMoreRecentLogs => RecentLogsTotalCount > _recentLogsLoaded;

    public LogEntry? SelectedRecentLog
    {
        get => _selectedRecentLog;
        set => SetProperty(ref _selectedRecentLog, value);
    }
    private LogEntry? _selectedRecentLog;

    public string LogSearchText
    {
        get => _logSearchText;
        set
        {
            if (SetProperty(ref _logSearchText, value))
                ApplyLogFilter();
        }
    }

    public string JobFilter
    {
        get => _jobFilter;
        set
        {
            if (SetProperty(ref _jobFilter, value))
                ApplyJobFilter();
        }
    }

    public JobModel? SelectedJob
    {
        get => _selectedJob;
        set => SetProperty(ref _selectedJob, value);
    }

    public bool IsTaskSchedulerRunning
    {
        get => _isTaskSchedulerRunning;
        set => SetProperty(ref _isTaskSchedulerRunning, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        set => SetProperty(ref _isPaused, value);
    }

    public bool IsUpcomingRunsExpanded
    {
        get => _isUpcomingRunsExpanded;
        set
        {
            if (SetProperty(ref _isUpcomingRunsExpanded, value) && value)
                IsRunLogsExpanded = false;
        }
    }

    public bool IsRunLogsExpanded
    {
        get => _isRunLogsExpanded;
        set
        {
            if (SetProperty(ref _isRunLogsExpanded, value) && value)
                IsUpcomingRunsExpanded = false;
        }
    }

    public int ActiveJobCount => Jobs.Count(j => j.Enabled);

    public ICommand AddJobCommand { get; }
    public ICommand EditJobCommand { get; }
    public ICommand DeleteJobCommand { get; }
    public ICommand RunNowCommand { get; }
    public ICommand ViewJobCommand { get; }
    public ICommand ToggleJobEnabledCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenRunPromptCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand LoadMoreRecentLogsCommand { get; }
    public ICommand ClearAllRecentLogsCommand { get; }
    public ICommand ViewRecentLogCommand { get; }
    public ICommand RerunRecentLogCommand { get; }
    public ICommand DeleteRecentLogCommand { get; }
    public ICommand ToggleUpcomingRunsCommand { get; }
    public ICommand ToggleRunLogsCommand { get; }

    public event Action? RequestAddJob;
    public event Action<JobModel>? RequestEditJob;
    public event Action<JobModel>? RequestViewJob;
    public event Action<JobModel>? RequestRunNow;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenRunPrompt;
    public event Action<LogEntry>? RequestViewLog;

    private readonly ITaskSchedulerService _taskSchedulerService;
    private readonly ILogService _logService;
    private readonly IAppLogService _appLogService;

    public MainViewModel() : this(App.TaskSchedulerService, App.LogService, App.AppLogService)
    {
    }

    public MainViewModel(ITaskSchedulerService taskSchedulerService, ILogService logService, IAppLogService appLogService)
    {
        _taskSchedulerService = taskSchedulerService;
        _logService = logService;
        _appLogService = appLogService;

        AddJobCommand = new RelayCommand(OnAddJob);
        EditJobCommand = new RelayCommand(_ => OnEditJob(), _ => SelectedJob != null);
        DeleteJobCommand = new AsyncRelayCommand(_ => OnDeleteJobAsync(), _ => SelectedJob != null);
        RunNowCommand = new AsyncRelayCommand(_ => OnRunNowAsync(), _ => SelectedJob != null);
        ViewJobCommand = new RelayCommand(_ => OnViewJob(), _ => SelectedJob != null);
        ToggleJobEnabledCommand = new AsyncRelayCommand(OnToggleJobEnabledAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenSettingsCommand = new RelayCommand(_ => RequestOpenSettings?.Invoke());
        OpenRunPromptCommand = new RelayCommand(_ => RequestOpenRunPrompt?.Invoke());
        PauseResumeCommand = new AsyncRelayCommand(OnPauseResumeAsync);
        ExitCommand = new RelayCommand(_ => OnExit());
        LoadMoreRecentLogsCommand = new RelayCommand(_ => LoadMoreRecentLogs(), _ => HasMoreRecentLogs);
        ClearAllRecentLogsCommand = new AsyncRelayCommand(_ => ClearAllRecentLogsAsync());

        // Commands updated to support parameters for row-based execution
        ViewRecentLogCommand = new RelayCommand(OnViewRecentLog);
        RerunRecentLogCommand = new RelayCommand(OnRerunRecentLog);
        DeleteRecentLogCommand = new AsyncRelayCommand(DeleteRecentLogAsync);
        ToggleUpcomingRunsCommand = new RelayCommand(_ => IsUpcomingRunsExpanded = !IsUpcomingRunsExpanded);
        ToggleRunLogsCommand = new RelayCommand(_ => IsRunLogsExpanded = !IsRunLogsExpanded);

        _agoUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _agoUpdateTimer.Tick += (s, e) => OnPropertyChanged(nameof(RecentLogs));
        _agoUpdateTimer.Start();

        _dataRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _dataRefreshTimer.Tick += async (s, e) => await RefreshAsync();
        _dataRefreshTimer.Start();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await RefreshAsync();

        // Check if Task Scheduler service is running and offer to start it
        if (!IsTaskSchedulerRunning)
        {
            var result = MessageBox.Show(
                "Windows Task Scheduler service is not running. Scheduled jobs will not execute.\n\n" +
                "Would you like to start the Task Scheduler service now?",
                "Task Scheduler Service Not Running",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var started = await _taskSchedulerService.TryStartServiceAsync();
                if (started)
                {
                    MessageBox.Show(
                        "Task Scheduler service started successfully.",
                        "Service Started",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    await RefreshAsync();
                }
                else
                {
                    MessageBox.Show(
                        "Failed to start Task Scheduler service. You may need administrator privileges.\n\n" +
                        "Please start the service manually using Services.msc or run this application as administrator.",
                        "Service Start Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }

    public async Task RefreshAsync()
    {
        // If already refreshing, queue another one to ensure we don't skip a priority update
        if (!await _refreshLock.WaitAsync(0))
        {
            _isRefreshQueued = true;
            return;
        }

        try
        {
            do
            {
                _isRefreshQueued = false;
                await PerformRefreshInternalAsync();

                // If another refresh was queued while we were working, wait a tiny bit
                // to allow concurrent database writes to finalize before we go again.
                if (_isRefreshQueued)
                {
                    await Task.Delay(500);
                }
            } while (_isRefreshQueued); // Repeat if a new request came in while we were working
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task PerformRefreshInternalAsync()
    {
        try
        {
            // Background fetching of Task Scheduler status and jobs
            var (isServiceRunning, allJobs) = await Task.Run(() =>
            {
                var running = _taskSchedulerService.IsServiceRunning();
                var jobs = _taskSchedulerService.GetAllJobs();
                return (running, jobs);
            });

            IsTaskSchedulerRunning = isServiceRunning;

            // Update Jobs collection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateCollection(Jobs, allJobs, (j1, j2) => j1.Name == j2.Name);
                ApplyJobFilter();
                OnPropertyChanged(nameof(ActiveJobCount));
            });

            // Load upcoming runs and recent logs
            await RefreshUpcomingRunsAndLogsAsync();
        }
        catch (Exception ex)
        {
            _appLogService.LogError(ex, "Failed to refresh main dashboard data");
        }
    }

    private async Task RefreshUpcomingRunsAndLogsAsync(bool clearExistingLogs = false)
    {
        if (clearExistingLogs)
        {
            _recentLogsLoaded = _recentLogsPageSize;
        }

        // Take a snapshot of jobs to process off-thread
        var currentJobs = Jobs.ToList();
        var searchText = LogSearchText;

        // Process data off the UI thread
        var (upcoming, logs, totalCount) = await Task.Run(async () =>
        {
            // Calculate upcoming runs
            var upcomingList = currentJobs
                .Where(j => j.NextRunTime.HasValue && j.Enabled)
                .OrderBy(j => j.NextRunTime)
                .Take(5)
                .Select(j => new UpcomingRun
                {
                    JobName = j.Name,
                    ScheduleDescription = ScheduleDescriptionHelper.GetDescription(j.Schedule),
                    NextRunTime = j.NextRunTime!.Value
                }).ToList();

            // Load recent logs from SQLite
            List<LogEntry> resultLogs;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                resultLogs = await _logService.GetRecentLogsAsync(_recentLogsLoaded);
            }
            else
            {
                // Offload search to SQL for $O(log N)$ performance
                resultLogs = await _logService.SearchLogsAsync(searchText, 100);
            }

            var count = await _logService.GetRecentLogCountAsync();

            // Trace logging for troubleshooting real-time sync
            if (resultLogs.Any())
            {
                var topIds = string.Join(", ", resultLogs.Take(5).Select(l => l.Id));
                System.Diagnostics.Trace.WriteLine($"[MainVM] Refresh fetched {resultLogs.Count} logs. Top IDs: {topIds}. Total: {count}");
                _appLogService.LogInfo("UI_REFRESH", $"Fetched {resultLogs.Count} logs. Top IDs: {topIds}. Total: {count}");
            }

            return (upcomingList, resultLogs, count);
        });

        // Update UI state
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            UpdateCollection(UpcomingRuns, upcoming, (u1, u2) => u1.JobName == u2.JobName && u1.NextRunTime == u2.NextRunTime);
            UpdateCollection(RecentLogs, logs, (l1, l2) => l1.Id == l2.Id || (l1.LogFilePath == l2.LogFilePath && !string.IsNullOrEmpty(l1.LogFilePath)));

            _recentLogsTotalCount = totalCount;

            OnPropertyChanged(nameof(RecentLogsTotalCount));
            OnPropertyChanged(nameof(RecentLogsLoadedCount));
            OnPropertyChanged(nameof(HasMoreRecentLogs));
        });
    }

    private int _recentLogsTotalCount;
    public int RecentLogsTotalCount => _recentLogsTotalCount;

    private async void LoadMoreRecentLogs()
    {
        _recentLogsLoaded += _recentLogsPageSize;
        await RefreshUpcomingRunsAndLogsAsync(false);
    }

    private async Task ClearAllRecentLogsAsync()
    {
        var dialog = new ConfirmDialog(
            "Clear All Logs",
            "Are you sure you want to delete all log files?\n\nThis action cannot be undone.",
            true)
        { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() != true) return;

        await _logService.ClearAllLogsAsync();
        _appLogService.LogInfo("LOGS_CLEARED", "All logs cleared");
        await RefreshAsync();
    }

    private void OnViewRecentLog(object? parameter)
    {
        if (parameter is LogEntry log)
            RequestViewLog?.Invoke(log);
        else if (SelectedRecentLog != null)
            RequestViewLog?.Invoke(SelectedRecentLog);
    }

    private void OnRerunRecentLog(object? parameter)
    {
        if (parameter is LogEntry log)
        {
            var job = Jobs.FirstOrDefault(j => j.Name == log.JobName);
            if (job != null)
                RequestRunNow?.Invoke(job);
        }
    }

    private async Task DeleteRecentLogAsync(object? parameter)
    {
        var logToDelete = parameter as LogEntry ?? SelectedRecentLog;
        if (logToDelete == null) return;

        var dialog = new ConfirmDialog(
            "Delete Log",
            "Are you sure you want to delete this log entry?",
            true)
        { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() != true) return;

        if (logToDelete.Id > 0)
            await _logService.DeleteLogAsync(logToDelete.Id);
        else if (!string.IsNullOrEmpty(logToDelete.LogFilePath))
            await _logService.DeleteLogAsync(logToDelete.LogFilePath);

        _appLogService.LogInfo("LOG_DELETE", $"Deleted log. ID: {logToDelete.Id}, Path: {logToDelete.LogFilePath}");
        await RefreshAsync();
    }

    private void ApplyJobFilter()
    {
        var searchText = JobFilter;
        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? Jobs.ToList()
            : Jobs.Where(j => j.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        UpdateCollection(FilteredJobs, filtered, (j1, j2) => j1.Name == j2.Name);
    }

    private void ApplyLogFilter()
    {
        // Reset count and refresh when search text changes
        _ = RefreshUpcomingRunsAndLogsAsync(true);
    }


    private void OnAddJob()
    {
        RequestAddJob?.Invoke();
    }

    private void OnEditJob()
    {
        if (SelectedJob != null)
            RequestEditJob?.Invoke(SelectedJob);
    }

    private async Task OnDeleteJobAsync()
    {
        if (SelectedJob == null) return;

        var dialog = new Views.ConfirmDialog(
            "Delete Job",
            "Are you sure you want to delete the job '{SelectedJob.Name}'?\n\nThis action cannot be undone.",
            true)
        { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() != true) return;

        await _taskSchedulerService.DeleteJobAsync(SelectedJob.Name);
        _appLogService.LogInfo("JOB_DELETE", $"Deleted job: {SelectedJob.Name}");
        await RefreshAsync();
    }

    private async Task OnRunNowAsync()
    {
        if (SelectedJob == null) return;
        RequestRunNow?.Invoke(SelectedJob);
        await Task.CompletedTask;
    }

    private void OnViewJob()
    {
        if (SelectedJob != null)
            RequestViewJob?.Invoke(SelectedJob);
    }

    private async Task OnToggleJobEnabledAsync(object? parameter)
    {
        if (parameter is not JobModel job) return;

        await _taskSchedulerService.SetJobEnabledAsync(job.Name, !job.Enabled);
        job.Enabled = !job.Enabled;
        OnPropertyChanged(nameof(ActiveJobCount));
        _appLogService.LogInfo("JOB_TOGGLE", $"Job '{job.Name}' enabled: {job.Enabled}");
        await RefreshUpcomingRunsAndLogsAsync();
    }

    private async Task OnPauseResumeAsync()
    {
        if (IsPaused)
        {
            await _taskSchedulerService.ResumeAllJobsAsync();
            IsPaused = false;
            _appLogService.LogInfo("JOBS_RESUMED", "All jobs resumed");
        }
        else
        {
            await _taskSchedulerService.PauseAllJobsAsync();
            IsPaused = true;
            _appLogService.LogInfo("JOBS_PAUSED", "All jobs paused");
        }
        await RefreshAsync();
    }

    private void OnExit()
    {
        _appLogService.LogInfo("APP_EXIT", "Application exited by user");
        Application.Current.Shutdown();
    }
}

public class UpcomingRun
{
    public string JobName { get; set; } = string.Empty;
    public string ScheduleDescription { get; set; } = string.Empty;
    public DateTime NextRunTime { get; set; }
}
