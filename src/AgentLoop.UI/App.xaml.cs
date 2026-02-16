using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using AgentLoop.Core.Interfaces;
using AgentLoop.Core.Services;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;
using AgentLoop.UI.ViewModels;
using AgentLoop.UI.Views;
using Hardcodet.Wpf.TaskbarNotification;
using System.Threading;
using System.Threading.Tasks;

namespace AgentLoop.UI;

public partial class App : Application
{
    // Services
    public static IConfigurationService ConfigService { get; private set; } = null!;
    public static IAgentCommandService AgentCommandService { get; private set; } = null!;
    public static ITaskSchedulerService TaskSchedulerService { get; private set; } = null!;
    public static ILogService LogService { get; private set; } = null!;
    public static IAppLogService AppLogService { get; private set; } = null!;
    public static NotificationService NotificationService { get; private set; } = null!;
    public static NavigationService NavigationService { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;

    private static TaskbarIcon? _trayIcon;
    private static MainWindow? _mainWindow;
    private static bool _isPaused;
    private static readonly CancellationTokenSource _appCts = new();
    private static System.Threading.Mutex? _appMutex;
    private const string AppMutexName = "Global\\AgentLoop_Active_Session";
    private static readonly SemaphoreSlim _taskProcessingSemaphore = new(1, 1);
    private static bool _isProcessingQueued;
    private static long _lastProcessedLogId;
    private static FileSystemWatcher? _dbWatcher;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for command-line job execution mode
        if (e.Args.Length > 0 && e.Args[0] == "--run-job")
        {
            // Only run if the main application is already active
            if (!System.Threading.Mutex.TryOpenExisting(AppMutexName, out _))
            {
                // Main app is not running, exit silently
                Environment.Exit(0);
                return;
            }

            try
            {
                ExecuteJobFromCommandLine(e.Args);
                Shutdown();
                return;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to execute job: {ex.Message}");
                Environment.Exit(1);
                return;
            }
        }

        // Create the mutex to signal that the main app is running
        _appMutex = new System.Threading.Mutex(true, AppMutexName);

        // Global exception handlers
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogService?.LogError(args.Exception, "Unhandled UI Exception");
            MessageBox.Show(
                $"An unexpected error occurred:\n{args.Exception.Message}",
                "AgentLoop Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLogService?.LogError(ex, "Unhandled Domain Exception");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogService?.LogError(args.Exception, "Unobserved Task Exception");
            args.SetObserved();
        };

        InitializeServices();
        _lastProcessedLogId = LogService.GetLastLogIdAsync().GetAwaiter().GetResult();

        ConfigService.UpdateStartupRegistration(Settings.StartWithWindows);
        InitializeTrayIcon();
        StartLogMaintenance();
        StartDatabaseWatcher();
        StartExternalChangeWatcher();
        ShowStartupWindow();
    }

    private void InitializeServices()
    {
        ConfigService = new ConfigurationService();
        Settings = ConfigService.LoadSettings();

        AppLogService = new AppLogService();
        AppLogService.SetDebugMode(Settings.DebugMode);
        AppLogService.LogInfo("APP_START", "Application started");

        AgentCommandService = new AgentCommandService();
        LogService = new LogService(Settings.LogsDirectory);
        TaskSchedulerService = new TaskSchedulerService(
            Settings.TaskFolder, Settings.AgentCommand, Settings.LogsDirectory);

        NotificationService = new NotificationService();
        NotificationService.NotificationRequested += OnNotificationRequested;
        NotificationService.NotificationClicked += OnNotificationClicked;

        NavigationService = new NavigationService(CreateViewModel);
    }

    private static void ExecuteJobFromCommandLine(string[] args)
    {
        // New Args Format: --run-job "jobName" --command "..." --prompt "..." --logs "..."
        if (args.Length < 2) Environment.Exit(0);

        string jobName = args[1];
        string agentCommand = "";
        string prompt = "";
        string logsDirectory = "";

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--command" && i + 1 < args.Length) agentCommand = args[++i];
            else if (args[i] == "--prompt" && i + 1 < args.Length) prompt = args[++i];
            else if (args[i] == "--logs" && i + 1 < args.Length) logsDirectory = args[++i];
        }

        if (string.IsNullOrEmpty(agentCommand) || string.IsNullOrEmpty(prompt))
        {
            if (args.Length >= 5)
            {
                try
                {
                    agentCommand = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(args[2]));
                    prompt = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(args[3]));
                    logsDirectory = args[4];
                }
                catch { }
            }
        }

        if (string.IsNullOrEmpty(agentCommand)) Environment.Exit(1);

        if (!string.IsNullOrEmpty(logsDirectory)) Directory.CreateDirectory(logsDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var safeJobName = string.Concat(jobName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var logFile = Path.Combine(logsDirectory, $"{safeJobName}_{timestamp}.log");

        var startTime = DateTime.Now;

        // Handle variable substitution in prompt before inserting into agent command
        var processedPrompt = prompt
            .Replace("{date}", startTime.ToString("yyyy-MM-dd"))
            .Replace("{time}", startTime.ToString("HH:mm:ss"))
            .Replace("{datetime}", startTime.ToString("yyyy-MM-dd HH:mm:ss"));

        var displayCommand = agentCommand.Replace("{prompt}", processedPrompt);
        var (executable, arguments) = ProcessHelper.ParseCommand(displayCommand);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true
        };

        var logBuilder = new System.Text.StringBuilder();
        logBuilder.AppendLine($"=== Job Run Started: {startTime:yyyy-MM-dd HH:mm:ss} ===");
        logBuilder.AppendLine($"Job: {jobName}");
        logBuilder.AppendLine($"Prompt: {prompt}");
        logBuilder.AppendLine($"Command: {displayCommand}");
        logBuilder.AppendLine();

        var stdoutBuilder = new System.Text.StringBuilder();
        var stderrBuilder = new System.Text.StringBuilder();
        int exitCode = -1;

        try
        {
            using var process = new Process { StartInfo = processStartInfo };
            process.OutputDataReceived += (s, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for up to 10 minutes for the agent to complete
            if (process.WaitForExit(600000))
            {
                exitCode = process.ExitCode;
            }
            else
            {
                process.Kill(true);
                exitCode = -1;
                stderrBuilder.AppendLine("Error: Command timed out after 10 minutes.");
            }
        }
        catch (Exception ex)
        {
            exitCode = -1;
            stderrBuilder.AppendLine($"Failed to execute command: {ex.Message}");
        }

        var endTime = DateTime.Now;

        var logEntry = new LogEntry
        {
            JobName = jobName,
            StartTime = startTime,
            EndTime = endTime,
            ExitCode = exitCode,
            Status = exitCode == 0 ? JobStatus.Success : JobStatus.Failure,
            Prompt = prompt,
            Command = displayCommand,
            StandardOutput = stdoutBuilder.ToString().Trim(),
            StandardError = stderrBuilder.ToString().Trim(),
            DurationSeconds = (endTime - startTime).TotalSeconds,
            AgentName = AgentLoop.Data.Models.AgentHelper.GetAgentNameFromCommand(displayCommand),
            LogFilePath = logFile // Keep reference to the legacy file path if needed
        };

        // Initialize LogService locally for CLI execution
        // Since it's a separate process, WAL mode on the DB is critical
        var cliLogService = new LogService(logsDirectory);
        cliLogService.InsertLogEntryAsync(logEntry).Wait();

        // Also write the physical file for backward compatibility if desired, 
        // but the DB is now the primary source.
        // File.WriteAllText(logFile, logBuilder.ToString());
        Environment.Exit(exitCode);
    }

    private static void OnNotificationRequested(object? sender, NotificationEventArgs e)
    {
        var title = e.IsSuccess ? $"Job Completed: {e.JobName}" : $"Job Failed: {e.JobName}";

        CustomNotificationManager.Show(title, e.Message, e.IsSuccess, e.Output, () =>
        {
            NotificationService.RaiseNotificationClicked(e.JobName);
        }, e.CustomColor, e.CustomIcon);

        // Refresh Main Window if it's open
        Current.Dispatcher.Invoke(() =>
        {
            if (_mainWindow?.DataContext is MainViewModel mainVm)
            {
                _ = mainVm.RefreshAsync();
            }
        });
    }

    private static void OnNotificationClicked(object? sender, string jobName)
    {
        Current.Dispatcher.Invoke(async () =>
        {
            // Find the latest log for this job and open LogViewerDialog
            var logs = await LogService.GetJobLogsAsync(jobName);
            var latestLog = logs.OrderByDescending(l => l.StartTime).FirstOrDefault();

            if (latestLog != null)
            {
                var dialog = new LogViewerDialog(latestLog);
                dialog.Show();
            }
            else
            {
                // Fallback to ViewJobDialog if no logs found
                var job = TaskSchedulerService.GetAllJobs().FirstOrDefault(j => j.Name == jobName);
                if (job != null)
                {
                    var dialog = new ViewJobDialog(job);
                    dialog.Show();
                }
            }
        });
    }

    private static void StartLogMaintenance()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await LogService.PurgeOldLogsAsync(Settings.LogRetentionDays);
                await LogService.RotateLogsAsync(Settings.LogMaxSizeMb);
                AppLogService.LogInfo("LOG_MAINTENANCE", "Log rotation and purge completed on startup");
            }
            catch (Exception ex)
            {
                AppLogService.LogError(ex, "Log maintenance failed on startup");
            }
        });
    }

    private static void StartDatabaseWatcher()
    {
        try
        {
            var logsDir = Settings.LogsDirectory;
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            _dbWatcher = new FileSystemWatcher(logsDir)
            {
                Filter = "agentloop_logs.db*", // Watch .db and -wal/-shm
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            _dbWatcher.Changed += OnDatabaseChanged;
            _dbWatcher.Created += OnDatabaseChanged;
            _dbWatcher.EnableRaisingEvents = true;

            AppLogService.LogInfo("WATCHER_START", "Database watcher started. Using reactive sync.");
        }
        catch (Exception ex)
        {
            AppLogService.LogError(ex, "Failed to start database watcher");
        }
    }

    private static void OnDatabaseChanged(object sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to let the write finish and locks to release
                await Task.Delay(500);
                await CheckForNewLogsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                AppLogService.LogError(ex, "Error handling database change event");
            }
        });
    }

    private static async Task CheckForNewLogsAsync(CancellationToken token)
    {
        if (!await _taskProcessingSemaphore.WaitAsync(0, token))
        {
            _isProcessingQueued = true;
            return;
        }

        try
        {
            do
            {
                _isProcessingQueued = false;
                await ProcessNewLogsInternalAsync(token);
            } while (_isProcessingQueued && !token.IsCancellationRequested);
        }
        finally
        {
            _taskProcessingSemaphore.Release();
        }
    }

    private static async Task ProcessNewLogsInternalAsync(CancellationToken token)
    {
        try
        {
            // Fetch all logs inserted since our last check
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(Settings.LogsDirectory, "agentloop_logs.db")};Default Timeout=5000;");
            await connection.OpenAsync(token);

            var newLogs = new List<LogEntry>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM Logs WHERE Id > $lastId ORDER BY Id ASC";
                cmd.Parameters.AddWithValue("$lastId", _lastProcessedLogId);

                using var reader = await cmd.ExecuteReaderAsync(token);
                var idOrdinal = reader.GetOrdinal("Id");
                var jobNameOrdinal = reader.GetOrdinal("JobName");
                var startTimeOrdinal = reader.GetOrdinal("StartTime");
                var statusOrdinal = reader.GetOrdinal("Status");
                var stdoutOrdinal = reader.GetOrdinal("StandardOutput");
                var stderrOrdinal = reader.GetOrdinal("StandardError");
                var exitCodeOrdinal = reader.GetOrdinal("ExitCode");
                var durationOrdinal = reader.GetOrdinal("DurationSeconds");

                while (await reader.ReadAsync(token))
                {
                    newLogs.Add(new LogEntry
                    {
                        Id = reader.GetInt64(idOrdinal),
                        JobName = reader.GetString(jobNameOrdinal),
                        StartTime = DateTime.Parse(reader.GetString(startTimeOrdinal)),
                        Status = (JobStatus)reader.GetInt32(statusOrdinal),
                        StandardOutput = reader.IsDBNull(stdoutOrdinal) ? "" : reader.GetString(stdoutOrdinal),
                        StandardError = reader.IsDBNull(stderrOrdinal) ? "" : reader.GetString(stderrOrdinal),
                        ExitCode = reader.GetInt32(exitCodeOrdinal),
                        DurationSeconds = reader.IsDBNull(durationOrdinal) ? 0 : reader.GetDouble(durationOrdinal)
                    });
                }
            }

            if (newLogs.Count == 0) return;

            var allJobs = TaskSchedulerService.GetAllJobs();

            foreach (var log in newLogs)
            {
                _lastProcessedLogId = Math.Max(_lastProcessedLogId, log.Id);

                var job = allJobs.FirstOrDefault(j => j.Name == log.JobName);
                if (job == null || job.Silent) continue;

                var checkSuccess = Settings.NotificationOnSuccess;
                var checkFailure = Settings.NotificationOnFailure;

                var isSuccess = log.Status == JobStatus.Success;
                var outputPreview = TruncateOutput(log.StandardOutput);

                if (isSuccess && checkSuccess)
                {
                    NotificationService.ShowSuccess(log.JobName, $"Completed successfully in {log.DurationSeconds:F1}s", outputPreview, job.HexColor, job.Icon);
                }
                else if (!isSuccess && checkFailure)
                {
                    NotificationService.ShowError(log.JobName, $"Failed with exit code {log.ExitCode}", outputPreview, job.HexColor, job.Icon);
                }

                _ = SaveJobResultToFileAsync(log.JobName, log);
            }

            // Refresh UI
            await Current.Dispatcher.InvokeAsync(async () =>
            {
                if (_mainWindow?.DataContext is MainViewModel mainVm)
                    await mainVm.RefreshAsync();

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is ViewJobDialog vjd && vjd.DataContext is ViewJobViewModel vjvm)
                    {
                        await vjvm.RefreshAsync();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            AppLogService.LogError(ex, "Error processing new database logs");
        }
    }

    private static string? TruncateOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var firstThree = lines.Take(3).ToList();

        var result = string.Join(" ", firstThree);
        if (result.Length > 150)
            result = result.Substring(0, 147) + "...";
        else if (lines.Length > 3)
            result += "...";

        return result;
    }

    private static async Task SaveJobResultToFileAsync(string jobName, LogEntry log)
    {
        try
        {
            var resultsDir = Settings.ResultsDirectory;
            if (string.IsNullOrWhiteSpace(resultsDir))
                return;

            // Sanitize job name for folder name
            var safeJobName = string.Join("_", jobName.Split(Path.GetInvalidFileNameChars()));
            var targetDir = Path.Combine(resultsDir, safeJobName);
            Directory.CreateDirectory(targetDir);

            var timestamp = log.StartTime.ToString("yyyyMMdd_HHmmss");
            var statusStr = log.Status == JobStatus.Success ? "SUCCESS" : "FAILED";
            var fileName = $"{timestamp}_{statusStr}.txt";
            var filePath = Path.Combine(targetDir, fileName);

            // Match the display logic in LogViewerDialog (Save only the actual response)
            var content = new StringBuilder();
            content.Append(log.StandardOutput.Trim());

            if (log.Status == JobStatus.Failure && !string.IsNullOrWhiteSpace(log.StandardError))
            {
                if (content.Length > 0) content.AppendLine().AppendLine();
                content.AppendLine("--- ERROR ---");
                content.Append(log.StandardError.Trim());
            }

            if (content.Length == 0)
            {
                content.Append("*No output captured.*");
            }

            await File.WriteAllTextAsync(filePath, content.ToString());
        }
        catch (Exception ex)
        {
            AppLogService.LogError(ex, $"Failed to auto-save result for job: {jobName}");
        }
    }

    private static void StartExternalChangeWatcher()
    {
        _ = WatchForExternalChangesAsync(_appCts.Token);
    }

    private static async Task WatchForExternalChangesAsync(CancellationToken token)
    {
        var lastJobNames = new HashSet<string>();

        // Initialize with current job names
        try
        {
            var jobs = TaskSchedulerService.GetAllJobs();
            lastJobNames = jobs.Select(j => j.Name).ToHashSet();
        }
        catch (Exception ex)
        {
            AppLogService.LogError(ex, "Failed to initialize external change watcher");
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), token);

                var currentJobs = TaskSchedulerService.GetAllJobs();
                var currentJobNames = currentJobs.Select(j => j.Name).ToHashSet();

                // Check if jobs were added or removed externally
                var added = currentJobNames.Except(lastJobNames).ToList();
                var removed = lastJobNames.Except(currentJobNames).ToList();

                if (added.Count > 0 || removed.Count > 0)
                {
                    AppLogService.LogInfo("EXTERNAL_CHANGE",
                        $"Detected external changes: {added.Count} added, {removed.Count} removed");

                    // Refresh the main window if it exists
                    Current.Dispatcher.Invoke(() =>
                    {
                        if (_mainWindow?.DataContext is MainViewModel vm)
                            _ = vm.RefreshAsync();
                    });
                }

                lastJobNames = currentJobNames;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogService.LogError(ex, "External change watcher error");
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void InitializeTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "AgentLoop",
            Icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/agentloop.ico")).Stream)
            // We handle the ContextMenu manually to fix the first-click dislocation bug
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        // Manual handling on MouseUp is more reliable for context menus
        _trayIcon.TrayRightMouseUp += OnTrayRightMouseUp;
    }

    private void OnTrayRightMouseUp(object? sender, RoutedEventArgs e)
    {
        var menu = CreateTrayContextMenu();

        // Critical for auto-hiding: make a window foreground before showing menu
        // This ensures the menu closes when clicking outside
        Window? refWindow = _mainWindow;
        if (refWindow == null || !refWindow.IsLoaded)
        {
            refWindow = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsLoaded);
        }

        if (refWindow != null)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(refWindow);
            SetForegroundWindow(helper.Handle);
        }

        // Use MousePoint for standard behavior (aligns with cursor, flips if near edge)
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "Show" };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) =>
        {
            var dialog = new Views.SettingsDialog();
            if (_mainWindow != null && _mainWindow.IsVisible)
            {
                dialog.Owner = _mainWindow;
            }
            dialog.ShowDialog();
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var pauseItem = new System.Windows.Controls.MenuItem { Header = "Pause All Jobs" };
        pauseItem.Click += async (_, _) => await PauseAllJobsAsync();
        menu.Items.Add(pauseItem);

        var resumeItem = new System.Windows.Controls.MenuItem { Header = "Resume All Jobs" };
        resumeItem.Click += async (_, _) => await ResumeAllJobsAsync();
        menu.Items.Add(resumeItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var runPromptItem = new System.Windows.Controls.MenuItem { Header = "Adhoc Job" };
        runPromptItem.Click += (_, _) =>
        {
            var dialog = new Views.RunPromptDialog();
            if (_mainWindow != null && _mainWindow.IsVisible)
            {
                dialog.Owner = _mainWindow;
            }
            dialog.ShowDialog();
        };
        menu.Items.Add(runPromptItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var aboutItem = new System.Windows.Controls.MenuItem { Header = "About" };
        aboutItem.Click += (_, _) =>
        {
            var dialog = new Views.AboutDialog();
            if (_mainWindow != null && _mainWindow.IsVisible)
            {
                dialog.Owner = _mainWindow;
            }
            dialog.ShowDialog();
        };
        menu.Items.Add(aboutItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private static ViewModelBase CreateViewModel(Type viewModelType)
    {
        if (viewModelType == typeof(MainViewModel))
            return new MainViewModel();
        if (viewModelType == typeof(SettingsViewModel))
            return new SettingsViewModel();
        if (viewModelType == typeof(RunPromptViewModel))
            return new RunPromptViewModel();

        throw new ArgumentException($"Unknown ViewModel type: {viewModelType.Name}");
    }

    private void ShowStartupWindow()
    {
        // Check if agent command is configured
        if (string.IsNullOrWhiteSpace(Settings.AgentCommand))
        {
            var setupDialog = new SetupDialog();
            var result = setupDialog.ShowDialog();

            if (result != true)
            {
                Shutdown();
                return;
            }

            // Reload settings after setup
            Settings = ConfigService.LoadSettings();
            TaskSchedulerService = new TaskSchedulerService(
                Settings.TaskFolder, Settings.AgentCommand, Settings.LogsDirectory);
        }

        _mainWindow = new MainWindow();

        var args = Environment.GetCommandLineArgs();
        bool startMinimized = Settings.StartMinimizedToTray || args.Contains("--minimized");

        if (!startMinimized)
        {
            _mainWindow.Show();
        }
    }

    public static void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private static async Task PauseAllJobsAsync()
    {
        await TaskSchedulerService.PauseAllJobsAsync();
        _isPaused = true;
        UpdateTrayTooltip();
        AppLogService.LogInfo("JOBS_PAUSED", "All jobs paused from tray");
        if (_mainWindow?.DataContext is MainViewModel vm)
            await vm.RefreshAsync();
    }

    private static async Task ResumeAllJobsAsync()
    {
        await TaskSchedulerService.ResumeAllJobsAsync();
        _isPaused = false;
        UpdateTrayTooltip();
        AppLogService.LogInfo("JOBS_RESUMED", "All jobs resumed from tray");
        if (_mainWindow?.DataContext is MainViewModel vm)
            await vm.RefreshAsync();
    }

    private static void UpdateTrayTooltip()
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = _isPaused ? "AgentLoop (Paused)" : "AgentLoop";
        }
    }

    public static void ExitApplication()
    {
        AppLogService.LogInfo("APP_EXIT", "Application exited");
        _appCts.Cancel();
        _trayIcon?.Dispose();

        _appMutex?.Dispose();

        Environment.Exit(0);
    }

    public static async Task ReloadSettingsAsync()
    {
        Settings = ConfigService.LoadSettings();
        AppLogService.SetDebugMode(Settings.DebugMode);

        if (TaskSchedulerService != null)
        {
            TaskSchedulerService.UpdateAgentCommand(Settings.AgentCommand);
            await TaskSchedulerService.UpdateAllJobsAsync();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appCts.Cancel();
        _trayIcon?.Dispose();
        _appMutex?.Dispose();
        base.OnExit(e);
    }

    private static string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"\x1B\[[0-9;]*[a-zA-Z]", "");
    }
}
