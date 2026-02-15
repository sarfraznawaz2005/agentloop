using System.Windows;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        DataContext = new AboutDialogViewModel { CloseAction = Close };

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version != null
            ? $"Version {version.Major}.{version.Minor}.{version.Build}"
            : "Version 1.0.0";
    }
}

public class AboutDialogViewModel : ViewModelBase
{
    public System.Action? CloseAction { get; set; }
    public System.Windows.Input.ICommand CloseCommand { get; }

    public AboutDialogViewModel()
    {
        CloseCommand = new Helpers.RelayCommand(_ => CloseAction?.Invoke());
    }
}
