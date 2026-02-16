using AgentLoop.Data.Models;

namespace AgentLoop.Core.Interfaces;

public interface IAgentCommandService
{
    string SubstitutePrompt(string agentCommand, string prompt);
    string SubstitutePromptForExecution(string agentCommand, string prompt);
    Task<(string Output, string Error, int ExitCode)> ExecuteCommandAsync(
        string command, CancellationToken cancellationToken = default);
    Task<(bool Success, string Output, string Error)> ValidateCommandAsync(
        string agentCommand, CancellationToken cancellationToken = default);
}
