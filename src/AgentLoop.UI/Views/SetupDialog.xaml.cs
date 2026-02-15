using System.Windows;
using System.Windows.Controls;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class SetupDialog : Window
{
    public SetupDialog()
    {
        InitializeComponent();

        if (DataContext is SetupDialogViewModel vm)
        {
            vm.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
