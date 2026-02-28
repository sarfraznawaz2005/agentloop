using System.ComponentModel;

namespace AgentLoop.Data.Models;

public class JobModel : INotifyPropertyChanged
{
    private bool _isRunning;

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

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
