using System.Windows;
using AgentLoop.Data.Models;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class AddEditJobDialog : Window
{
    public AddEditJobDialog() : this(null) { }

    public AddEditJobDialog(JobModel? existingJob)
    {
        InitializeComponent();
        DataContext = new AddEditJobViewModel(existingJob);

        if (DataContext is AddEditJobViewModel vm)
        {
            vm.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };

            vm.RequestTestRun += prompt =>
            {
                // Open RunJobDialog with the test prompt
                var runJobDialog = new RunJobDialog("(Test Run)", prompt) { Owner = this };
                runJobDialog.ShowDialog();
            };
        }
    }
}
