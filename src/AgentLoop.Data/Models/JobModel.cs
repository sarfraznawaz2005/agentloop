namespace AgentLoop.Data.Models;

public class JobModel
{
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public ScheduleModel Schedule { get; set; } = new();
    public bool Silent { get; set; }
    public bool Enabled { get; set; } = true;
    public string? AgentOverride { get; set; }
    public string? HexColor { get; set; }
    public string? Icon { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
}
