using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AgentLoop.UI.Helpers;
using FontAwesome.Sharp;

namespace AgentLoop.UI.ViewModels;

public class ConfirmDialogViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _message = string.Empty;
    private IconChar _icon = IconChar.ExclamationTriangle;
    private Brush _iconBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ea4335"));

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public IconChar Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public Brush IconBackground
    {
        get => _iconBackground;
        set => SetProperty(ref _iconBackground, value);
    }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public Action<bool>? CloseAction { get; set; }

    public ConfirmDialogViewModel(string title, string message, bool isDanger = true)
    {
        Title = title;
        Message = message;

        if (isDanger)
        {
            Icon = IconChar.ExclamationTriangle;
            IconBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ea4335"));
        }
        else
        {
            Icon = IconChar.QuestionCircle;
            IconBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a73e8"));
        }

        ConfirmCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
        CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
    }
}
