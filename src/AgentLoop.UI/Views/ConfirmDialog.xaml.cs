using System.Windows;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, bool isDanger = true)
    {
        InitializeComponent();
        DataContext = new ConfirmDialogViewModel(title, message, isDanger)
        {
            CloseAction = result =>
            {
                DialogResult = result;
                Close();
            }
        };
    }
}
