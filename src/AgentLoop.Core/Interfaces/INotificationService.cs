namespace AgentLoop.Core.Interfaces;

public interface INotificationService
{
    void ShowSuccess(string jobName, string message, string? output = null, string? customColor = null, string? customIcon = null);
    void ShowError(string jobName, string message, string? output = null, string? customColor = null, string? customIcon = null);
    event EventHandler<string>? NotificationClicked;
}
