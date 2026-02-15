using AgentLoop.Core.Interfaces;

namespace AgentLoop.Core.Services;

public class NotificationService : INotificationService
{
    public event EventHandler<string>? NotificationClicked;
    public event EventHandler<NotificationEventArgs>? NotificationRequested;

    public void ShowSuccess(string jobName, string message, string? output = null, string? customColor = null, string? customIcon = null)
    {
        NotificationRequested?.Invoke(this, new NotificationEventArgs(jobName, message, true, output, customColor, customIcon));
    }

    public void ShowError(string jobName, string message, string? output = null, string? customColor = null, string? customIcon = null)
    {
        NotificationRequested?.Invoke(this, new NotificationEventArgs(jobName, message, false, output, customColor, customIcon));
    }

    public void RaiseNotificationClicked(string jobName)
    {
        NotificationClicked?.Invoke(this, jobName);
    }
}

public class NotificationEventArgs : EventArgs
{
    public string JobName { get; }
    public string Message { get; }
    public bool IsSuccess { get; }
    public string? Output { get; }
    public string? CustomColor { get; }
    public string? CustomIcon { get; }

    public NotificationEventArgs(string jobName, string message, bool isSuccess, string? output = null, string? customColor = null, string? customIcon = null)
    {
        JobName = jobName;
        Message = message;
        IsSuccess = isSuccess;
        Output = output;
        CustomColor = customColor;
        CustomIcon = customIcon;
    }
}
