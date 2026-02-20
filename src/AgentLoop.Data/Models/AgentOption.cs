namespace AgentLoop.Data.Models;

public class AgentOption
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;

    public override string ToString() => Name;
}
