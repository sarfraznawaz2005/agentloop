using System.Windows;
using System.Windows.Media;

namespace AgentLoop.UI.Views;

public partial class NotificationWindow : Window
{
    private readonly Action? _onClick;

    public NotificationWindow(string title, string message, bool isSuccess, string? output, Action? onClick, string? customColor = null, string? customIcon = null)
    {
        InitializeComponent();
        _onClick = onClick;

        TitleText.Text = title;
        MessageText.Text = message;

        if (!string.IsNullOrEmpty(output))
        {
            OutputText.Text = output;
            OutputBorder.Visibility = Visibility.Visible;
        }

        if (isSuccess)
        {
            var bgColor = string.IsNullOrEmpty(customColor) ? "#00A300" : customColor;
            MainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor));

            if (!string.IsNullOrEmpty(customIcon))
            {
                if (Enum.TryParse<FontAwesome.Sharp.IconChar>(customIcon, true, out var iconChar))
                    StatusIcon.Icon = iconChar;
                else
                    StatusIcon.Icon = FontAwesome.Sharp.IconChar.CheckCircle;
            }
            else
            {
                StatusIcon.Icon = FontAwesome.Sharp.IconChar.CheckCircle;
            }
        }
        else
        {
            MainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A30000"));

            if (!string.IsNullOrEmpty(customIcon))
            {
                if (Enum.TryParse<FontAwesome.Sharp.IconChar>(customIcon, true, out var iconChar))
                    StatusIcon.Icon = iconChar;
                else
                    StatusIcon.Icon = FontAwesome.Sharp.IconChar.TimesCircle;
            }
            else
            {
                StatusIcon.Icon = FontAwesome.Sharp.IconChar.TimesCircle;
            }
        }
    }

    private void OnWindowClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _onClick?.Invoke();
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }
}
