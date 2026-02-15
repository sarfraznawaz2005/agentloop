using System.Windows;
using System.Windows.Input;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;
using System.IO;
using System.Collections.ObjectModel;

namespace AgentLoop.UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private string _customAgentCommand = string.Empty;
    private string _predefinedAgentCommand = string.Empty;
    private bool _notificationOnSuccess;
    private bool _notificationOnFailure;
    private int _logMaxSizeMb;
    private int _logRetentionDays;
    private bool _startMinimizedToTray;
    private bool _startWithWindows;
    private string _resultsDirectory = string.Empty;
    private bool _debugMode;
    private bool _isTesting;
    private string _testResult = string.Empty;
    private bool _hasTestResult;
    private bool _testSuccess;
    private bool _isVerified = true;

    private bool _isCustomCommand;
    private bool _isPredefinedAgent;
    private AgentOption? _selectedAgent;

    public ObservableCollection<AgentOption> PredefinedAgents { get; } = new(AgentLoop.Data.Models.AgentHelper.PredefinedAgents);

    public bool IsCustomCommand
    {
        get => _isCustomCommand;
        set
        {
            if (SetProperty(ref _isCustomCommand, value) && value)
            {
                IsPredefinedAgent = false;
                ResetVerification();
                OnPropertyChanged(nameof(AgentCommand));
            }
        }
    }

    public bool IsPredefinedAgent
    {
        get => _isPredefinedAgent;
        set
        {
            if (SetProperty(ref _isPredefinedAgent, value) && value)
            {
                IsCustomCommand = false;
                ResetVerification();
                OnPropertyChanged(nameof(AgentCommand));
            }
        }
    }

    public AgentOption? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (SetProperty(ref _selectedAgent, value))
            {
                if (value != null)
                {
                    PredefinedAgentCommand = value.Command;
                }
                ResetVerification();
            }
        }
    }

    public string PredefinedAgentCommand
    {
        get => _predefinedAgentCommand;
        set
        {
            if (SetProperty(ref _predefinedAgentCommand, value))
            {
                ResetVerification();
                OnPropertyChanged(nameof(AgentCommand));
            }
        }
    }

    public string CustomAgentCommand
    {
        get => _customAgentCommand;
        set
        {
            if (SetProperty(ref _customAgentCommand, value))
            {
                ResetVerification();
                OnPropertyChanged(nameof(AgentCommand));
            }
        }
    }

    public string AgentCommand => IsPredefinedAgent ? PredefinedAgentCommand : CustomAgentCommand;

    private void ResetVerification()
    {
        HasTestResult = false;
        IsVerified = false;
        CommandManager.InvalidateRequerySuggested();
    }

    public bool IsVerified
    {
        get => _isVerified;
        private set
        {
            if (SetProperty(ref _isVerified, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public bool NotificationOnSuccess
    {
        get => _notificationOnSuccess;
        set => SetProperty(ref _notificationOnSuccess, value);
    }

    public bool NotificationOnFailure
    {
        get => _notificationOnFailure;
        set => SetProperty(ref _notificationOnFailure, value);
    }

    public int LogMaxSizeMb
    {
        get => _logMaxSizeMb;
        set => SetProperty(ref _logMaxSizeMb, Math.Clamp(value, 1, 100));
    }

    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set => SetProperty(ref _logRetentionDays, Math.Clamp(value, 1, 365));
    }

    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set => SetProperty(ref _startMinimizedToTray, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public string ResultsDirectory
    {
        get => _resultsDirectory;
        set => SetProperty(ref _resultsDirectory, value);
    }

    public bool DebugMode
    {
        get => _debugMode;
        set => SetProperty(ref _debugMode, value);
    }

    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            if (SetProperty(ref _isTesting, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string TestResult
    {
        get => _testResult;
        set => SetProperty(ref _testResult, value);
    }

    public bool HasTestResult
    {
        get => _hasTestResult;
        set => SetProperty(ref _hasTestResult, value);
    }

    public bool TestSuccess
    {
        get => _testSuccess;
        set => SetProperty(ref _testSuccess, value);
    }

    public string TaskFolder => App.Settings.TaskFolder;
    public string LogsDirectory => App.Settings.LogsDirectory;
    public string AppLogPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgentLoop", "app.log");

    public bool CanSave => IsVerified && !string.IsNullOrWhiteSpace(AgentCommand)
                           && AgentCommand.Contains("{prompt}");

    public bool CanTest => !string.IsNullOrWhiteSpace(AgentCommand)
                           && AgentCommand.Contains("{prompt}")
                           && !IsTesting;

    // Commands
    public ICommand TestCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand BrowseResultsDirectoryCommand { get; }

    public event Action<bool>? RequestClose;

    public SettingsViewModel()
    {
        LoadSettings();

        TestCommand = new AsyncRelayCommand(TestAgentCommandAsync, () => CanTest);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
        BrowseResultsDirectoryCommand = new RelayCommand(_ => BrowseResultsDirectory());
    }

    private void BrowseResultsDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            InitialDirectory = ResultsDirectory,
            Title = "Select Results Directory"
        };

        if (dialog.ShowDialog() == true)
        {
            ResultsDirectory = dialog.FolderName;
        }
    }

    private void LoadSettings()
    {
        var settings = App.Settings;
        var currentCommand = settings.AgentCommand;

        NotificationOnSuccess = settings.NotificationOnSuccess;
        NotificationOnFailure = settings.NotificationOnFailure;
        LogMaxSizeMb = settings.LogMaxSizeMb;
        LogRetentionDays = settings.LogRetentionDays;
        StartMinimizedToTray = settings.StartMinimizedToTray;
        StartWithWindows = settings.StartWithWindows;
        ResultsDirectory = settings.ResultsDirectory;
        DebugMode = settings.DebugMode;

        // Try to match current command to predefined list
        var matched = PredefinedAgents.FirstOrDefault(a => a.Command == currentCommand);
        if (matched != null)
        {
            _selectedAgent = matched;
            _isPredefinedAgent = true;
            _isCustomCommand = false;
            _predefinedAgentCommand = currentCommand;
            _customAgentCommand = string.Empty;
        }
        else
        {
            _isPredefinedAgent = false;
            _isCustomCommand = true;
            _customAgentCommand = currentCommand;
            _predefinedAgentCommand = PredefinedAgents[0].Command;
            _selectedAgent = PredefinedAgents[0];
        }

        IsVerified = true; // Initial load is verified
    }

    private async Task TestAgentCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(AgentCommand)) return;

        IsTesting = true;
        HasTestResult = false;
        IsVerified = false;

        try
        {
            var (success, output, error) = await App.AgentCommandService
                .ValidateCommandAsync(AgentCommand);

            TestSuccess = success;
            IsVerified = success;
            TestResult = success
                ? $"Success!\n\n{output}"
                : $"Failed!\n\n{(string.IsNullOrEmpty(error) ? "No output received or command failed." : error)}";
            HasTestResult = true;
        }
        catch (Exception ex)
        {
            TestSuccess = false;
            IsVerified = false;
            TestResult = $"Error: {ex.Message}";
            HasTestResult = true;
        }
        finally
        {
            IsTesting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;

        var settings = new AppSettings
        {
            AgentCommand = AgentCommand,
            NotificationOnSuccess = NotificationOnSuccess,
            NotificationOnFailure = NotificationOnFailure,
            LogMaxSizeMb = LogMaxSizeMb,
            LogRetentionDays = LogRetentionDays,
            StartMinimizedToTray = StartMinimizedToTray,
            StartWithWindows = StartWithWindows,
            ResultsDirectory = ResultsDirectory,
            DebugMode = DebugMode,
            TaskFolder = App.Settings.TaskFolder,
            LogsDirectory = App.Settings.LogsDirectory,
        };

        await App.ConfigService.SaveSettingsAsync(settings);
        App.ConfigService.UpdateStartupRegistration(StartWithWindows);
        await App.ReloadSettingsAsync();

        App.AppLogService.LogInfo("SETTINGS_CHANGE", "Settings saved and all tasks updated");
        RequestClose?.Invoke(true);
    }

    private void ResetDefaults()
    {
        NotificationOnSuccess = true;
        NotificationOnFailure = true;
        LogMaxSizeMb = 2;
        LogRetentionDays = 30;
        StartMinimizedToTray = false;
        StartWithWindows = true;
        ResultsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgentLoop");
        DebugMode = false;

        // Reset to predefined
        SelectedAgent = PredefinedAgents[0];
        IsPredefinedAgent = true;
    }
}
