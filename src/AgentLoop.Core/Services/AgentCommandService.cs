using System.Diagnostics;
using System.Text;
using AgentLoop.Core.Interfaces;

namespace AgentLoop.Core.Services;

public class AgentCommandService : IAgentCommandService
{
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(30);

    public string SubstitutePrompt(string agentCommand, string prompt)
    {
        var now = DateTime.Now;
        var processedPrompt = prompt
            .Replace("{date}", now.ToString("yyyy-MM-dd"))
            .Replace("{time}", now.ToString("HH:mm:ss"))
            .Replace("{datetime}", now.ToString("yyyy-MM-dd HH:mm:ss"));

        return agentCommand.Replace("{prompt}", processedPrompt);
    }

    public async Task<(string Output, string Error, int ExitCode)> ExecuteCommandAsync(
        string command, CancellationToken cancellationToken = default)
    {
        var (executable, arguments) = ParseCommand(command);
        var resolvedExe = ResolveExecutablePath(executable);

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

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stdout.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return (stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd(), process.ExitCode);
    }

    public async Task<(bool Success, string Output, string Error)> ValidateCommandAsync(
        string agentCommand, CancellationToken cancellationToken = default)
    {
        var testCommand = SubstitutePrompt(agentCommand, "Hello");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ValidationTimeout);

        try
        {
            var (output, error, exitCode) = await ExecuteCommandAsync(testCommand, cts.Token);

            bool success = exitCode == 0;

            // Check for common error indicators in output/error streams
            if (success)
            {
                var combined = (output + " " + error).ToLowerInvariant();
                if (combined.Contains("error:") ||
                    combined.Contains("failed:") ||
                    combined.Contains("authentication failed") ||
                    combined.Contains("api key not found") ||
                    combined.Contains("command not found"))
                {
                    success = false;
                }
            }

            return (success, output, error);
        }
        catch (OperationCanceledException)
        {
            return (false, string.Empty, "Validation timed out after 30 seconds.");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Failed to execute command: {ex.Message}");
        }
    }

    public static (string Executable, string Arguments) ParseCommand(string command)
    {
        command = command.Trim();
        if (string.IsNullOrEmpty(command))
            throw new ArgumentException("Command cannot be empty.", nameof(command));

        string executable;
        string arguments;

        if (command.StartsWith('"'))
        {
            var closingQuote = command.IndexOf('"', 1);
            if (closingQuote < 0)
                throw new ArgumentException("Unterminated quote in command.", nameof(command));

            executable = command[1..closingQuote];
            arguments = command[(closingQuote + 1)..].TrimStart();
        }
        else
        {
            var spaceIndex = command.IndexOf(' ');
            if (spaceIndex < 0)
            {
                executable = command;
                arguments = string.Empty;
            }
            else
            {
                executable = command[..spaceIndex];
                arguments = command[(spaceIndex + 1)..].TrimStart();
            }
        }

        return (executable, arguments);
    }

    public static string ResolveExecutablePath(string executable)
    {
        // If it's already a rooted or relative path with extension, use directly
        if (Path.IsPathRooted(executable) && File.Exists(executable))
            return executable;

        if (Path.HasExtension(executable))
        {
            // Try to find in PATH
            var found = FindInPath(executable);
            if (found != null) return found;
            return executable;
        }

        // No extension — try with PATHEXT extensions
        var pathExtVar = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        var extensions = pathExtVar.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var ext in extensions)
        {
            var withExt = executable + ext;
            var found = FindInPath(withExt);
            if (found != null) return found;
        }

        // Fallback: return as-is and let the OS try
        return executable;
    }

    private static string? FindInPath(string fileName)
    {
        // Check current directory first
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var dirs = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in dirs)
        {
            var fullPath = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
