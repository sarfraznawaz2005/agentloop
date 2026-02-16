using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;
using AgentLoop.Core.Helpers;

namespace AgentLoop.UI.ViewModels;

public class AddEditJobViewModel : ViewModelBase
{
    private readonly JobModel? _existingJob;
    private readonly string? _originalJobName;
    private string _jobName = string.Empty;
    private string _prompt = string.Empty;
    private ScheduleType _scheduleType = ScheduleType.Daily;
    private int _minuteInterval = 15;
    private int _hourlyInterval = 1;
    private int _dailyRecurringInterval = 2;
    private TimeSpan _runTime = new(9, 0, 0);
    private TimeSpan _startTime = new(8, 0, 0);
    private bool _hasTimeRestrictions;
    private TimeSpan _timeWindowStart = new(8, 0, 0);
    private TimeSpan _timeWindowEnd = new(18, 0, 0);
    private bool _silentMode;
    private bool _enabled = true;
    private bool _isSaving;
    private bool _useGlobalAgentCommand;
    private string _customAgentCommand = string.Empty;
    private string _predefinedAgentCommand = string.Empty;
    private bool _isCustomCommand;
    private bool _isPredefinedAgent;
    private AgentOption? _selectedAgent;
    private string? _selectedColor;
    private string? _selectedIcon;

    public ObservableCollection<string> AvailableColors { get; } = new()
    {
        "#00A300", // Green (Default)
        "#007ACC", // Blue
        "#68217A", // Purple
        "#CA5010", // Orange
        "#008272", // Teal
        "#505050"  // Gray
    };

    public ObservableCollection<string> AvailableIcons { get; } = new()
    {
        "CheckCircle",
        "Rocket",
        "Bell",
        "Calendar",
        "Clock",
        "Cog",
        "Terminal",
        "Database",
        "Cloud",
        "Robot"
    };

    public string? SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    public string? SelectedIcon
    {
        get => _selectedIcon;
        set => SetProperty(ref _selectedIcon, value);
    }

    public bool IsEditMode => _existingJob != null;
    public string WindowTitle => IsEditMode ? $"Edit Job: {_existingJob!.Name}" : "Add New Job";

    public ObservableCollection<AgentOption> PredefinedAgents { get; } = new(AgentLoop.Data.Models.AgentHelper.PredefinedAgents);

    public bool UseGlobalAgentCommand
    {
        get => _useGlobalAgentCommand;
        set => SetProperty(ref _useGlobalAgentCommand, value);
    }

    public bool IsCustomCommand
    {
        get => _isCustomCommand;
        set
        {
            if (SetProperty(ref _isCustomCommand, value) && value)
                IsPredefinedAgent = false;
        }
    }

    public bool IsPredefinedAgent
    {
        get => _isPredefinedAgent;
        set
        {
            if (SetProperty(ref _isPredefinedAgent, value) && value)
                IsCustomCommand = false;
        }
    }

    public AgentOption? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (SetProperty(ref _selectedAgent, value) && value != null)
                PredefinedAgentCommand = value.Command;
        }
    }

    public string PredefinedAgentCommand
    {
        get => _predefinedAgentCommand;
        set => SetProperty(ref _predefinedAgentCommand, value);
    }

    public string CustomAgentCommand
    {
        get => _customAgentCommand;
        set => SetProperty(ref _customAgentCommand, value);
    }

    public string AgentOverride => IsPredefinedAgent ? PredefinedAgentCommand : CustomAgentCommand;

    public string GlobalAgentCommandHint => $"System Default: {App.Settings.AgentCommand}";

    public string JobName
    {
        get => _jobName;
        set
        {
            if (SetProperty(ref _jobName, value))
                OnPropertyChanged(nameof(CanSave));
        }
    }

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (SetProperty(ref _prompt, value))
                OnPropertyChanged(nameof(CanSave));
        }
    }

    public ScheduleType ScheduleType
    {
        get => _scheduleType;
        set
        {
            if (SetProperty(ref _scheduleType, value))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public int MinuteInterval
    {
        get => _minuteInterval;
        set
        {
            if (SetProperty(ref _minuteInterval, Math.Clamp(value, 1, 59)))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public int HourlyInterval
    {
        get => _hourlyInterval;
        set
        {
            if (SetProperty(ref _hourlyInterval, Math.Clamp(value, 1, 12)))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public int DailyRecurringInterval
    {
        get => _dailyRecurringInterval;
        set
        {
            if (SetProperty(ref _dailyRecurringInterval, Math.Clamp(value, 1, 12)))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public TimeSpan RunTime
    {
        get => _runTime;
        set
        {
            if (SetProperty(ref _runTime, value))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public TimeSpan StartTime
    {
        get => _startTime;
        set
        {
            if (SetProperty(ref _startTime, value))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public bool HasTimeRestrictions
    {
        get => _hasTimeRestrictions;
        set
        {
            if (SetProperty(ref _hasTimeRestrictions, value))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public TimeSpan TimeWindowStart
    {
        get => _timeWindowStart;
        set
        {
            if (SetProperty(ref _timeWindowStart, value))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public TimeSpan TimeWindowEnd
    {
        get => _timeWindowEnd;
        set
        {
            if (SetProperty(ref _timeWindowEnd, value))
                OnPropertyChanged(nameof(SchedulePreview));
        }
    }

    public bool SilentMode
    {
        get => _silentMode;
        set => SetProperty(ref _silentMode, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        set => SetProperty(ref _isSaving, value);
    }

    // Day selections for Weekly
    public ObservableCollection<DaySelection> WeekDays { get; } =
    [
        new() { Day = DayOfWeek.Monday, Label = "M" },
        new() { Day = DayOfWeek.Tuesday, Label = "T" },
        new() { Day = DayOfWeek.Wednesday, Label = "W" },
        new() { Day = DayOfWeek.Thursday, Label = "T" },
        new() { Day = DayOfWeek.Friday, Label = "F" },
        new() { Day = DayOfWeek.Saturday, Label = "S" },
        new() { Day = DayOfWeek.Sunday, Label = "S" }
    ];

    // Day selections for time restrictions
    public ObservableCollection<DaySelection> RestrictedDays { get; } =
    [
        new() { Day = DayOfWeek.Monday, Label = "M", IsSelected = true },
        new() { Day = DayOfWeek.Tuesday, Label = "T", IsSelected = true },
        new() { Day = DayOfWeek.Wednesday, Label = "W", IsSelected = true },
        new() { Day = DayOfWeek.Thursday, Label = "T", IsSelected = true },
        new() { Day = DayOfWeek.Friday, Label = "F", IsSelected = true },
        new() { Day = DayOfWeek.Saturday, Label = "S" },
        new() { Day = DayOfWeek.Sunday, Label = "S" }
    ];

    // Monthly date selections (1-31 + Last Day)
    public ObservableCollection<DateSelection> MonthDays { get; } = [];

    public bool CanSave => !string.IsNullOrWhiteSpace(JobName) && !string.IsNullOrWhiteSpace(Prompt);

    public string SchedulePreview => ScheduleDescriptionHelper.GetDescription(BuildScheduleModel());

    // Commands
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand TestRunCommand { get; }

    public event Action<bool>? RequestClose;
    public event Action<string, string?>? RequestTestRun;

    public AddEditJobViewModel() : this(null) { }

    public AddEditJobViewModel(JobModel? existingJob)
    {
        _existingJob = existingJob;

        // Initialize month days
        for (int i = 1; i <= 31; i++)
            MonthDays.Add(new DateSelection { Date = i, Label = i.ToString() });
        MonthDays.Add(new DateSelection { Date = 32, Label = "Last" });

        // Initialize defaults for agent
        _useGlobalAgentCommand = true;
        _isPredefinedAgent = true;
        _selectedAgent = PredefinedAgents.FirstOrDefault(a => a.Command == App.Settings.AgentCommand) ?? PredefinedAgents[0];
        _predefinedAgentCommand = _selectedAgent.Command;
        _selectedColor = "#00A300";
        _selectedIcon = "CheckCircle";

        if (existingJob != null)
        {
            _originalJobName = existingJob.Name;
            LoadFromJob(existingJob);
        }

        // Subscribe to day selection changes
        foreach (var day in WeekDays)
            day.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SchedulePreview));
        foreach (var day in RestrictedDays)
            day.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SchedulePreview));
        foreach (var date in MonthDays)
            date.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SchedulePreview));

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave && !IsSaving);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        TestRunCommand = new AsyncRelayCommand(TestRunAsync, () => CanSave && !IsSaving);
    }

    private void LoadFromJob(JobModel job)
    {
        JobName = job.Name;
        Prompt = job.Prompt;
        SilentMode = job.Silent;
        Enabled = job.Enabled;
        ScheduleType = job.Schedule.Type;
        RunTime = job.Schedule.RunTime;
        SelectedColor = job.HexColor ?? "#00A300";
        SelectedIcon = job.Icon ?? "CheckCircle";

        switch (job.Schedule.Type)
        {
            case ScheduleType.Minute:
                MinuteInterval = job.Schedule.Interval;
                break;
            case ScheduleType.Hourly:
                HourlyInterval = job.Schedule.Interval;
                HasTimeRestrictions = job.Schedule.HasTimeRestrictions;
                if (job.Schedule.TimeWindowStart.HasValue)
                    TimeWindowStart = job.Schedule.TimeWindowStart.Value;
                if (job.Schedule.TimeWindowEnd.HasValue)
                    TimeWindowEnd = job.Schedule.TimeWindowEnd.Value;
                LoadRestrictedDays(job.Schedule.RestrictedDays);
                break;
            case ScheduleType.DailyRecurring:
                DailyRecurringInterval = job.Schedule.Interval;
                StartTime = job.Schedule.StartTime ?? new TimeSpan(8, 0, 0);
                HasTimeRestrictions = job.Schedule.HasTimeRestrictions;
                if (job.Schedule.TimeWindowStart.HasValue)
                    TimeWindowStart = job.Schedule.TimeWindowStart.Value;
                if (job.Schedule.TimeWindowEnd.HasValue)
                    TimeWindowEnd = job.Schedule.TimeWindowEnd.Value;
                LoadRestrictedDays(job.Schedule.RestrictedDays);
                break;
            case ScheduleType.Weekly:
                LoadWeekDays(job.Schedule.DaysOfWeek);
                break;
            case ScheduleType.MonthlyDates:
                LoadMonthDays(job.Schedule.MonthDays);
                break;
        }

        // Agent Override logic
        if (string.IsNullOrEmpty(job.AgentOverride))
        {
            UseGlobalAgentCommand = true;
            IsPredefinedAgent = true;
            SelectedAgent = PredefinedAgents.FirstOrDefault(a => a.Command == App.Settings.AgentCommand) ?? PredefinedAgents[0];
            CustomAgentCommand = string.Empty;
        }
        else
        {
            UseGlobalAgentCommand = false;
            var matched = PredefinedAgents.FirstOrDefault(a => a.Command == job.AgentOverride);
            if (matched != null)
            {
                IsPredefinedAgent = true;
                SelectedAgent = matched;
            }
            else
            {
                IsCustomCommand = true;
                CustomAgentCommand = job.AgentOverride;
                // Pre-fill predefined if someone switches
                SelectedAgent = PredefinedAgents[0];
            }
        }
    }

    private void LoadWeekDays(List<DayOfWeek> days)
    {
        foreach (var selection in WeekDays)
            selection.IsSelected = days.Contains(selection.Day);
    }

    private void LoadRestrictedDays(List<DayOfWeek> days)
    {
        foreach (var selection in RestrictedDays)
            selection.IsSelected = days.Contains(selection.Day);
    }

    private void LoadMonthDays(List<int> days)
    {
        foreach (var selection in MonthDays)
            selection.IsSelected = days.Contains(selection.Date);
    }

    private ScheduleModel BuildScheduleModel()
    {
        var model = new ScheduleModel { Type = ScheduleType };

        switch (ScheduleType)
        {
            case ScheduleType.Minute:
                model.Interval = MinuteInterval;
                break;
            case ScheduleType.Hourly:
                model.Interval = HourlyInterval;
                model.HasTimeRestrictions = HasTimeRestrictions;
                if (HasTimeRestrictions)
                {
                    model.RestrictedDays = RestrictedDays.Where(d => d.IsSelected).Select(d => d.Day).ToList();
                    model.TimeWindowStart = TimeWindowStart;
                    model.TimeWindowEnd = TimeWindowEnd;
                }
                break;
            case ScheduleType.Daily:
                model.RunTime = RunTime;
                break;
            case ScheduleType.DailyRecurring:
                model.Interval = DailyRecurringInterval;
                model.StartTime = StartTime;
                model.HasTimeRestrictions = HasTimeRestrictions;
                if (HasTimeRestrictions)
                {
                    model.RestrictedDays = RestrictedDays.Where(d => d.IsSelected).Select(d => d.Day).ToList();
                    model.TimeWindowStart = TimeWindowStart;
                    model.TimeWindowEnd = TimeWindowEnd;
                }
                break;
            case ScheduleType.Weekly:
                model.DaysOfWeek = WeekDays.Where(d => d.IsSelected).Select(d => d.Day).ToList();
                model.RunTime = RunTime;
                break;
            case ScheduleType.MonthlyDates:
                model.MonthDays = MonthDays.Where(d => d.IsSelected).Select(d => d.Date).ToList();
                model.RunTime = RunTime;
                break;
        }

        return model;
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;

        var existingJobs = await Task.Run(() => App.TaskSchedulerService.GetAllJobs());

        if (!IsEditMode)
        {
            if (existingJobs.Any(j => j.Name.Equals(JobName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A job with the name '{JobName}' already exists.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else if (_originalJobName != null && !JobName.Equals(_originalJobName, StringComparison.OrdinalIgnoreCase))
        {
            if (existingJobs.Any(j => j.Name.Equals(JobName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A job with the name '{JobName}' already exists.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        IsSaving = true;
        try
        {
            var job = new JobModel
            {
                Name = JobName,
                Prompt = Prompt,
                Schedule = BuildScheduleModel(),
                Silent = SilentMode,
                Enabled = Enabled,
                AgentOverride = UseGlobalAgentCommand ? null : AgentOverride,
                HexColor = SelectedColor,
                Icon = SelectedIcon
            };

            if (IsEditMode && _originalJobName != null)
            {
                await App.TaskSchedulerService.UpdateJobAsync(_originalJobName, job);
                App.AppLogService.LogInfo("JOB_EDIT", $"Updated job: {_originalJobName} -> {job.Name}");
            }
            else
            {
                await App.TaskSchedulerService.CreateJobAsync(job);
                App.AppLogService.LogInfo("JOB_CREATE", $"Created job: {job.Name}");
            }

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            App.AppLogService.LogError(ex, "Failed to save job");
            MessageBox.Show($"Failed to save job: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task TestRunAsync()
    {
        // Raise event to open Run Job dialog with the current prompt and agent override
        RequestTestRun?.Invoke(Prompt, UseGlobalAgentCommand ? null : AgentOverride);
        await Task.CompletedTask;
    }
}

public class DaySelection : ViewModelBase
{
    private bool _isSelected;

    public DayOfWeek Day { get; init; }
    public string Label { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public class DateSelection : ViewModelBase
{
    private bool _isSelected;

    public int Date { get; init; }
    public string Label { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
