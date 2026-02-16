using System.Collections.Generic;
using System.Linq;

namespace AgentLoop.Data.Models;

public static class AgentHelper
{
    public static List<AgentOption> PredefinedAgents { get; } = new()
    {
        new AgentOption { Name = "Claude", Command = "claude -p \"{prompt}\" --dangerously-skip-permissions" },
        new AgentOption { Name = "Codex", Command = "codex exec \"{prompt}\" --yolo" },
        new AgentOption { Name = "Gemini CLI", Command = "gemini -p \"{prompt}\" --approval-mode=yolo" },
        new AgentOption { Name = "OpenCode", Command = "opencode run \"{prompt}\"" },
        new AgentOption { Name = "Qwen Code", Command = "qwen -p \"{prompt}\" --approval-mode yolo" }
    };

    public static string GetAgentNameFromCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return "Unknown";

        // Extract the executable name (first part of the command)
        string firstPart = command.Trim().Split(' ')[0];
        string exe = firstPart.ToLower();

        var matched = PredefinedAgents.FirstOrDefault(a =>
        {
            string agentExe = a.Command.Trim().Split(' ')[0].ToLower();
            return exe == agentExe;
        });

        if (matched != null) return matched.Name;

        try
        {
            var exeName = System.IO.Path.GetFileNameWithoutExtension(firstPart);
            if (string.IsNullOrEmpty(exeName)) return "Custom";
            
            // Capitalize first letter
            return char.ToUpper(exeName[0]) + exeName[1..].ToLower();
        }
        catch
        {
            return "Custom";
        }
    }
}
