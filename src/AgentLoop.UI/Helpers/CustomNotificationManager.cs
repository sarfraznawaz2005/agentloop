using System.Windows;
using System.Windows.Threading;
using AgentLoop.UI.Views;

namespace AgentLoop.UI.Helpers;

public static class CustomNotificationManager
{
    private static readonly List<NotificationWindow> _openNotifications = new();
    private const double MaxNotifications = 5;
    private const double MarginRight = 10;
    private const double MarginBottom = 10;

    public static void Show(string title, string message, bool isSuccess, string? output = null, Action? onClick = null, string? customColor = null, string? customIcon = null)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = new NotificationWindow(title, message, isSuccess, output, onClick, customColor, customIcon);

            _openNotifications.Add(window);
            window.Closed += (s, e) =>
            {
                _openNotifications.Remove(window);
                Rearrange();
            };

            window.Show();
            Rearrange();

            // Auto close after 15 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                window.Close();
            };
            timer.Start();
        }));
    }

    private static void Rearrange()
    {
        double primaryScreenHeight = SystemParameters.WorkArea.Height;
        double primaryScreenWidth = SystemParameters.WorkArea.Width;
        double currentY = primaryScreenHeight - MarginBottom;

        // Take only the latest 5 to avoid filling the screen
        var visible = _openNotifications.TakeLast((int)MaxNotifications).ToList();

        foreach (var window in visible)
        {
            window.Left = primaryScreenWidth - window.Width - MarginRight;
            window.Top = currentY - window.Height;
            currentY -= window.Height;
        }

        // Close any that exceeded the max count
        var overflow = _openNotifications.Except(visible).ToList();
        foreach (var window in overflow)
        {
            window.Close();
        }
    }
}
