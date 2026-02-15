using System.Windows;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class RunPromptDialog : Window
{
    public RunPromptDialog()
    {
        InitializeComponent();

        if (DataContext is RunPromptViewModel vm)
        {
            vm.RequestRunPrompt += OnRunPrompt;
        }
    }

    private void OnRunPrompt(string name, string prompt)
    {
        var dialog = new RunJobDialog(name, prompt) { Owner = this };
        dialog.ShowDialog();

        // Refresh the main window if it's the owner or if we can find it
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow?.DataContext is MainViewModel vm)
        {
            _ = vm.RefreshAsync();
        }
    }
}
