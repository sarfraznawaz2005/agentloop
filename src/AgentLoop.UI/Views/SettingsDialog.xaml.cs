using System.Windows;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();

        if (DataContext is SettingsViewModel vm)
        {
            vm.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
