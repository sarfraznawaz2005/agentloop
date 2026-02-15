using System.IO;

namespace AgentLoop.Core.Services;

/// <summary>
/// Resolves executable names against the system PATH and PATHEXT environment variables.
/// This is needed because Process.Start with UseShellExecute=false does NOT search PATH.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Resolves an executable name to its full path by searching the system PATH.
    /// Returns the original name if it already exists as-is or contains a path separator.
    /// </summary>
    public static string ResolveExecutable(string executable)
    {
        // If it's already a rooted/relative path that exists, use it directly
        if (File.Exists(executable))
            return Path.GetFullPath(executable);

        // If it contains a directory separator, it's a relative/absolute path — don't search PATH
        if (executable.Contains(Path.DirectorySeparatorChar) ||
            executable.Contains(Path.AltDirectorySeparatorChar))
            return executable;

        // Get PATHEXT extensions (Windows: .COM;.EXE;.BAT;.CMD;.VBS;.JS;.WSH;.MSC;.PS1 etc.)
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        var extensions = pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries);

        // Check if the executable already has an extension that matches PATHEXT
        var hasKnownExtension = extensions.Any(ext =>
            executable.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

        // Get PATH directories
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var directories = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in directories)
        {
            if (hasKnownExtension)
            {
                // Already has a known extension (e.g., "tool.exe") — try exact match only
                var fullPath = Path.Combine(dir, executable);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            else
            {
                // No extension — try PATHEXT extensions first (e.g., agent.cmd before agent).
                // This matches Windows behavior: extensionless Unix shims like "agent" in
                // npm's bin folder are not valid Windows executables, but "agent.cmd" is.
                foreach (var ext in extensions)
                {
                    var withExt = Path.Combine(dir, executable + ext);
                    if (File.Exists(withExt))
                        return withExt;
                }
            }
        }

        // Not found — return as-is and let Process.Start give a meaningful error
        return executable;
    }

    /// <summary>
    /// Parses a command string into executable and arguments.
    /// Handles quoted executables (e.g., "C:\Program Files\tool.exe" --flag).
    /// </summary>
    public static (string executable, string arguments) ParseCommand(string command)
    {
        command = command.Trim();

        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote > 0)
            {
                var exe = command[1..endQuote];
                var args = command.Length > endQuote + 1 ? command[(endQuote + 2)..] : string.Empty;
                return (ResolveExecutable(exe), args);
            }
        }

        var spaceIndex = command.IndexOf(' ');
        if (spaceIndex > 0)
        {
            var exe = command[..spaceIndex];
            var args = command[(spaceIndex + 1)..];
            return (ResolveExecutable(exe), args);
        }

        return (ResolveExecutable(command), string.Empty);
    }
}
