using System.Text;
using AgentLoop.Data.Models;

namespace AgentLoop.Data.Services;

public static class JobMetadataHelper
{
    private const string HeaderLine = "AgentLoop Job v1";

    public static string Encode(JobModel job)
    {
        var promptBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(job.Prompt));
        var sb = new StringBuilder();
        sb.Append($"{HeaderLine}\nSILENT:{job.Silent.ToString().ToLower()}\nPROMPT:{promptBase64}");

        if (!string.IsNullOrWhiteSpace(job.AgentOverride))
        {
            var agentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(job.AgentOverride));
            sb.Append($"\nAGENT:{agentBase64}");
        }

        if (!string.IsNullOrWhiteSpace(job.HexColor))
        {
            sb.Append($"\nCOLOR:{job.HexColor}");
        }

        if (!string.IsNullOrWhiteSpace(job.Icon))
        {
            sb.Append($"\nICON:{job.Icon}");
        }

        return sb.ToString();
    }

    public static (string Prompt, bool Silent, string? AgentOverride, string? HexColor, string? Icon)? Decode(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var lines = description.Split('\n', StringSplitOptions.TrimEntries);
        if (lines.Length < 3 || lines[0] != HeaderLine)
            return null;

        bool silent = false;
        string prompt = string.Empty;
        string? agentOverride = null;
        string? hexColor = null;
        string? icon = null;

        foreach (var line in lines.Skip(1))
        {
            if (line.StartsWith("SILENT:", StringComparison.OrdinalIgnoreCase))
            {
                silent = line[7..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("PROMPT:", StringComparison.OrdinalIgnoreCase))
            {
                var base64 = line[7..].Trim();
                try
                {
                    prompt = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                }
                catch (FormatException)
                {
                    return null;
                }
            }
            else if (line.StartsWith("AGENT:", StringComparison.OrdinalIgnoreCase))
            {
                var base64 = line[6..].Trim();
                try
                {
                    agentOverride = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                }
                catch (FormatException)
                {
                    // Ignore malformed agent override
                }
            }
            else if (line.StartsWith("COLOR:", StringComparison.OrdinalIgnoreCase))
            {
                hexColor = line[6..].Trim();
            }
            else if (line.StartsWith("ICON:", StringComparison.OrdinalIgnoreCase))
            {
                icon = line[5..].Trim();
            }
        }

        if (string.IsNullOrEmpty(prompt))
            return null;

        return (prompt, silent, agentOverride, hexColor, icon);
    }

    public static bool IsAgentLoopTask(string? description)
    {
        return !string.IsNullOrWhiteSpace(description)
            && description.TrimStart().StartsWith(HeaderLine);
    }
}
