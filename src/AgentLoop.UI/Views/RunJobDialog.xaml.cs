using System.Windows;
using AgentLoop.Data.Models;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class RunJobDialog : Window
{
    private readonly RunJobViewModel _viewModel;

    public RunJobDialog(string jobName, string prompt, string? agentOverride = null)
    {
        InitializeComponent();
        _viewModel = new RunJobViewModel(jobName, prompt, agentOverride);
        DataContext = _viewModel;

        _viewModel.RequestClose += Close;
        _viewModel.RequestViewLog += OnViewLog;

        Loaded += async (_, _) => await _viewModel.RunAsync();
    }

    public RunJobDialog(JobModel job) : this(job.Name, job.Prompt, job.AgentOverride) { }

    private void OnViewLog(LogEntry log)
    {
        var dialog = new LogViewerDialog(log) { Owner = this };
        dialog.ShowDialog();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Allow closing even during run
        base.OnClosing(e);
    }

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(MarkdownViewer.Markdown);
        MessageBox.Show("Copied to clipboard.", "AgentLoop",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            FileName = $"Result_{_viewModel.JobName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(sfd.FileName, MarkdownViewer.Markdown);
                MessageBox.Show("Result exported successfully.", "AgentLoop",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
