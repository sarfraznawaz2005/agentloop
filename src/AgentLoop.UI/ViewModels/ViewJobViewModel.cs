using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;
using AgentLoop.UI.Views;

namespace AgentLoop.UI.ViewModels;

public class ViewJobViewModel : ViewModelBase
{
    private readonly JobModel _job;
    private string _logFilter = "All";
    private LogEntry? _selectedLog;
    private int _pageSize = 10;
    private int _loaded = 10;
    private readonly System.Threading.SemaphoreSlim _refreshLock = new(1, 1);
    private bool _isRefreshQueued;

    public string JobName => _job.Name;
    public string Prompt => _job.Prompt;
    public string? AgentOverride => string.IsNullOrWhiteSpace(_job.AgentOverride) 
        ? null 
        : char.ToUpper(_job.AgentOverride[0]) + _job.AgentOverride[1..].ToLower();
    public string ScheduleDescription => GetScheduleDescription();
    public DateTime? NextRun { get; private set; }
    public DateTime? LastRun { get; private set; }
    public JobStatus? LastStatus { get; private set; }

    public int TotalRuns { get; private set; }
    public int SuccessCount { get; private set; }
    public int FailedCount { get; private set; }

    public ObservableCollection<LogEntry> AllLogs { get; } = [];
    public ObservableCollection<LogEntry> FilteredLogs { get; } = [];

    public int FilteredLogCount => FilteredLogs.Count;
    public int TotalLogCount => AllLogs.Count;
    private List<LogEntry> _filteredLogsList = [];
    public bool HasMoreLogs => _filteredLogsList.Count > _loaded;

    public string LogFilter
    {
        get => _logFilter;
        set
        {
            if (SetProperty(ref _logFilter, value))
            {
                _loaded = _pageSize;
                ApplyLogFilter();
            }
        }
    }

    public LogEntry? SelectedLog
    {
        get => _selectedLog;
        set => SetProperty(ref _selectedLog, value);
    }

    public ICommand DeleteJobCommand { get; }
    public ICommand ViewLogCommand { get; }
    public ICommand RerunCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand DeleteLogCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }

    public event Action? RequestBack;
    public event Action<LogEntry>? RequestViewLog;
    public event Action<JobModel>? RequestRunJob;

    public ViewJobViewModel(JobModel job)
    {
        _job = job;

        DeleteJobCommand = new AsyncRelayCommand(_ => DeleteJobAsync());
        ViewLogCommand = new RelayCommand(OnViewLog);
        RerunCommand = new RelayCommand(_ => RequestRunJob?.Invoke(_job));
        BackCommand = new RelayCommand(_ => RequestBack?.Invoke());
        LoadMoreCommand = new RelayCommand(_ => LoadMore(), _ => HasMoreLogs);
        ClearAllCommand = new AsyncRelayCommand(_ => ClearAllLogsAsync());
        DeleteLogCommand = new AsyncRelayCommand(DeleteLogAsync);
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);

        _ = LoadDataAsync();
    }

    public async Task RefreshAsync()
    {
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
                await LoadDataAsync();
            } while (_isRefreshQueued);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task LoadDataAsync()
    {
        NextRun = App.TaskSchedulerService.GetNextRunTime(_job.Name);
        LastRun = App.TaskSchedulerService.GetLastRunTime(_job.Name);

        OnPropertyChanged(nameof(NextRun));
        OnPropertyChanged(nameof(LastRun));

        var logs = (await App.LogService.GetJobLogsAsync(_job.Name)).OrderByDescending(l => l.StartTime).ToList();
        UpdateCollection(AllLogs, logs, (l1, l2) => l1.Id == l2.Id || (l1.LogFilePath == l2.LogFilePath && !string.IsNullOrEmpty(l1.LogFilePath)));

        TotalRuns = AllLogs.Count;
        SuccessCount = AllLogs.Count(l => l.Status == JobStatus.Success);
        FailedCount = AllLogs.Count(l => l.Status == JobStatus.Failure);

        if (AllLogs.Count > 0)
            LastStatus = AllLogs[0].Status;

        OnPropertyChanged(nameof(TotalRuns));
        OnPropertyChanged(nameof(SuccessCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(LastStatus));
        OnPropertyChanged(nameof(TotalLogCount));

        _loaded = _pageSize;
        ApplyLogFilter();
    }

    private void ApplyLogFilter()
    {
        _filteredLogsList = LogFilter switch
        {
            "Success" => AllLogs.Where(l => l.Status == JobStatus.Success).ToList(),
            "Failed" => AllLogs.Where(l => l.Status == JobStatus.Failure).ToList(),
            "Favorites" => AllLogs.Where(l => l.IsFavorite).ToList(),
            _ => AllLogs.ToList()
        };

        UpdateCollection(FilteredLogs, _filteredLogsList.Take(_loaded).ToList(), (l1, l2) => l1.Id == l2.Id || (l1.LogFilePath == l2.LogFilePath && !string.IsNullOrEmpty(l1.LogFilePath)));

        OnPropertyChanged(nameof(FilteredLogCount));
        OnPropertyChanged(nameof(HasMoreLogs));
    }

    private void LoadMore()
    {
        _loaded += _pageSize;
        var toAdd = _filteredLogsList.Skip(FilteredLogs.Count).Take(_pageSize);
        foreach (var log in toAdd)
            FilteredLogs.Add(log);

        OnPropertyChanged(nameof(FilteredLogCount));
        OnPropertyChanged(nameof(HasMoreLogs));
    }

    private void OnViewLog(object? parameter)
    {
        if (parameter is LogEntry log)
            RequestViewLog?.Invoke(log);
        else if (SelectedLog != null)
            RequestViewLog?.Invoke(SelectedLog);
    }

    private async Task DeleteLogAsync(object? parameter)
    {
        var logToDelete = parameter as LogEntry ?? SelectedLog;
        if (logToDelete == null) return;

        var dialog = new ConfirmDialog(
            "Delete Log",
            "Are you sure you want to delete this log entry?",
            true)
        { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() != true) return;

        await App.LogService.DeleteLogAsync(logToDelete.LogFilePath);
        App.AppLogService.LogInfo("LOG_DELETE", $"Deleted log: {logToDelete.LogFilePath}");
        await LoadDataAsync();
    }

    private async Task ClearAllLogsAsync()
    {
        var dialog = new ConfirmDialog(
            "Clear All Logs",
            "Are you sure you want to delete all log entries for this job?\n\nFavorited logs will be preserved. This action cannot be undone.",
            true)
        { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() != true) return;

        await App.LogService.ClearJobLogsAsync(_job.Name);
        App.AppLogService.LogInfo("LOGS_CLEARED", $"Cleared all logs for job: {_job.Name}");
        await LoadDataAsync();
    }

    private async Task ToggleFavoriteAsync(object? parameter)
    {
        if (parameter is not LogEntry log || log.Id <= 0) return;

        var newState = !log.IsFavorite;
        await App.LogService.ToggleFavoriteAsync(log.Id, newState);
        await LoadDataAsync();
    }

    private async Task DeleteJobAsync()
    {
        var dialog = new ConfirmDialog(
            "Delete Job",
            "Are you sure you want to delete the job '{_job.Name}'?\n\nThis action cannot be undone.",
            true)
        { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() != true) return;

        await App.TaskSchedulerService.DeleteJobAsync(_job.Name);
        App.AppLogService.LogInfo("JOB_DELETE", $"Deleted job: {_job.Name}");
        RequestBack?.Invoke();
    }

    private string GetScheduleDescription()
    {
        var s = _job.Schedule;
        return s.Type switch
        {
            ScheduleType.Minute => $"Every {s.Interval} minute(s)",
            ScheduleType.Hourly => $"Every {s.Interval} hour(s)",
            ScheduleType.Daily => $"Daily at {s.RunTime:hh\\:mm}",
            ScheduleType.DailyRecurring => $"Every {s.Interval} hour(s) starting at {s.StartTime:hh\\:mm}",
            ScheduleType.Weekly => $"Weekly on {string.Join(", ", s.DaysOfWeek.Select(d => d.ToString()[..3]))} at {s.RunTime:hh\\:mm}",
            ScheduleType.MonthlyDates => $"Monthly on day(s) {string.Join(", ", s.MonthDays)} at {s.RunTime:hh\\:mm}",
            _ => "Unknown"
        };
    }
}
