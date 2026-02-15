using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;

namespace AgentLoop.UI.ViewModels;

public class SetupDialogViewModel : ViewModelBase
{
    private string _customAgentCommand = string.Empty;
    private string _predefinedAgentCommand = string.Empty;
    private bool _isTesting;
    private string _testResult = string.Empty;
    private bool _hasTestResult;
    private bool _testSuccess;
    private bool _isVerified;

    private bool _isCustomCommand;
    private bool _isPredefinedAgent = true;
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

    public bool CanSave => IsVerified && !string.IsNullOrWhiteSpace(AgentCommand)
                           && AgentCommand.Contains("{prompt}");

    public bool CanTest => !string.IsNullOrWhiteSpace(AgentCommand)
                           && AgentCommand.Contains("{prompt}")
                           && !IsTesting;

    public ICommand TestCommand { get; }
    public ICommand SaveCommand { get; }

    public event Action<bool>? RequestClose;

    public SetupDialogViewModel()
    {
        TestCommand = new AsyncRelayCommand(TestAgentCommandAsync, () => CanTest);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);

        // Default selection
        SelectedAgent = PredefinedAgents[0];
    }

    private async Task TestAgentCommandAsync()
    {
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

        var settings = App.Settings;
        settings.AgentCommand = AgentCommand;
        await App.ConfigService.SaveSettingsAsync(settings);
        App.AppLogService.LogInfo("SETTINGS_CHANGE", "Agent command configured during setup");

        RequestClose?.Invoke(true);
    }

    public void SetExample(string command)
    {
        CustomAgentCommand = command;
    }
}
