using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;

namespace AgentLoop.UI.ViewModels;

public class RunJobViewModel : ViewModelBase
{
    private readonly string _jobName;
    private readonly string _prompt;
    private string _output = string.Empty;
    private RunStatus _status = RunStatus.Running;
    private double _elapsedSeconds;
    private CancellationTokenSource? _cts;
    private DateTime _startTime;
    private LogEntry? _lastLog;

    public string JobName => _jobName;
    public string Prompt => _prompt;

    public string Output
    {
        get => _output;
        private set => SetProperty(ref _output, value);
    }

    public string WindowTitle => $"{(Status == RunStatus.Running ? "Running" : "Ran")}: {_jobName}";

    public RunStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsCompleted));
            }
        }
    }

    public double ElapsedSeconds
    {
        get => _elapsedSeconds;
        private set => SetProperty(ref _elapsedSeconds, value);
    }

    public bool IsRunning => Status == RunStatus.Running;
    public bool IsCompleted => Status != RunStatus.Running;

    // Commands
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ViewLogCommand { get; }
    public ICommand RerunCommand { get; }

    public event Action? RequestClose;
    public event Action<LogEntry>? RequestViewLog;

    private readonly string? _agentOverride;

    public RunJobViewModel(string jobName, string prompt, string? agentOverride = null)
    {
        _jobName = jobName;
        _prompt = prompt;
        _agentOverride = agentOverride;

        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsRunning);
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        ViewLogCommand = new RelayCommand(_ => OnViewLog(), _ => IsCompleted && _lastLog != null);
        RerunCommand = new AsyncRelayCommand(_ => RunAsync(), _ => IsCompleted);
    }

    public async Task RunAsync()
    {
        Status = RunStatus.Running;
        Output = string.Empty;
        _startTime = DateTime.Now;
        _cts = new CancellationTokenSource();

        var outputBuilder = new StringBuilder();
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        try
        {
            // Start elapsed timer
            var timerTask = UpdateElapsedAsync(_cts.Token);

            var agentCommand = _agentOverride ?? App.Settings.AgentCommand;
            var command = App.AgentCommandService.SubstitutePrompt(agentCommand, _prompt);

            // Execute command with live output
            var (executable, arguments) = Core.Services.AgentCommandService.ParseCommand(command);
            var resolvedExe = Core.Services.AgentCommandService.ResolveExecutablePath(executable);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = resolvedExe,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdoutBuilder.AppendLine(e.Data);
                    UpdateLiveOutput(stdoutBuilder, stderrBuilder);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stderrBuilder.AppendLine(e.Data);
                    UpdateLiveOutput(stdoutBuilder, stderrBuilder);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(_cts.Token);

            var endTime = DateTime.Now;
            ElapsedSeconds = (endTime - _startTime).TotalSeconds;

            Status = process.ExitCode == 0 ? RunStatus.Completed : RunStatus.Failed;

            // Final update to filter stderr if success
            UpdateLiveOutput(stdoutBuilder, stderrBuilder, Status == RunStatus.Failed);

            // Save to database
            _lastLog = new LogEntry
            {
                JobName = _jobName,
                StartTime = _startTime,
                EndTime = endTime,
                ExitCode = process.ExitCode,
                Status = Status == RunStatus.Completed ? JobStatus.Success : JobStatus.Failure,
                Prompt = _prompt,
                Command = command,
                StandardOutput = stdoutBuilder.ToString().TrimEnd(),
                StandardError = stderrBuilder.ToString().TrimEnd(),
                DurationSeconds = ElapsedSeconds,
                AgentName = AgentHelper.GetAgentNameFromCommand(command)
            };

            await App.LogService.InsertLogEntryAsync(_lastLog);

            App.AppLogService.LogInfo("JOB_RUN",
                $"Manual run of '{_jobName}' completed with exit code {process.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            Status = RunStatus.Cancelled;
        }
        catch (Exception ex)
        {
            Status = RunStatus.Failed;
            Output = $"Error: {ex.Message}";
            App.AppLogService.LogError(ex, $"Error running job '{_jobName}'");
        }
        finally
        {
            _cts?.Cancel();
        }
    }

    private void UpdateLiveOutput(StringBuilder stdout, StringBuilder stderr, bool forceShowError = true)
    {
        var cleanStdout = StripAnsiCodes(stdout.ToString().Trim());
        var cleanStderr = StripAnsiCodes(stderr.ToString().Trim());

        if (!forceShowError || string.IsNullOrEmpty(cleanStderr))
        {
            Output = cleanStdout;
        }
        else
        {
            Output = cleanStdout + (string.IsNullOrEmpty(cleanStdout) ? "" : "\n\n--- ERROR ---\n") + cleanStderr;
        }
    }

    private static string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, @"\x1B\[[0-9;]*[a-zA-Z]", "");
    }

    private async Task UpdateElapsedAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            ElapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnViewLog()
    {
        if (_lastLog != null)
            RequestViewLog?.Invoke(_lastLog);
    }
}

public enum RunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}
