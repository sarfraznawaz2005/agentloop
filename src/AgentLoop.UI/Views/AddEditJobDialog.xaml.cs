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

            vm.RequestTestRun += (prompt, agentOverride) =>
            {
                // Open RunJobDialog with the test prompt and agent override
                var runJobDialog = new RunJobDialog("(Test Run)", prompt, agentOverride) { Owner = this };
                runJobDialog.ShowDialog();
            };
        }
    }
}
