using System.Windows;
using AgentLoop.Data.Models;

namespace AgentLoop.UI.Views;

public partial class LogViewerDialog : Window
{
    public LogViewerDialog(LogEntry log)
    {
        InitializeComponent();
        _ = LoadLogDataAsync(log);
    }

    private async Task LoadLogDataAsync(LogEntry log)
    {
        LogEntry? fullLog = null;
        if (log.Id > 0)
            fullLog = await App.LogService.GetLogEntryByIdAsync(log.Id);

        if (fullLog == null && !string.IsNullOrEmpty(log.LogFilePath))
            fullLog = await App.LogService.GetLogEntryAsync(log.LogFilePath);

        // If data is missing, try once more after a small delay
        if (fullLog == null || fullLog.StartTime == default)
        {
            await Task.Delay(200);
            if (log.Id > 0)
                fullLog = await App.LogService.GetLogEntryByIdAsync(log.Id);
            else if (!string.IsNullOrEmpty(log.LogFilePath))
                fullLog = await App.LogService.GetLogEntryAsync(log.LogFilePath);
        }

        fullLog ??= log;

        JobNameText.Text = fullLog.JobName;
        TimestampText.Text = fullLog.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        AgentNameText.Text = fullLog.AgentName;
        DurationText.Text = $"{fullLog.DurationSeconds:F1}s";

        // Set status dot color
        StatusDot.Fill = (System.Windows.Media.Brush)Application.Current.FindResource(
            fullLog.Status == JobStatus.Success ? "AccentBrush" : "ErrorBrush");

        if (fullLog.Id > 0)
        {
            var content = await App.LogService.ReadStdoutContentByIdAsync(fullLog.Id);
            MarkdownViewer.Markdown = string.IsNullOrEmpty(content) ? "*No output captured.*" : content;
        }
        else
        {
            await LoadLogContentAsync(fullLog.LogFilePath);
        }
    }

    private async Task LoadLogContentAsync(string logFilePath)
    {
        try
        {
            var content = await App.LogService.ReadStdoutContentAsync(logFilePath);
            MarkdownViewer.Markdown = string.IsNullOrEmpty(content)
                ? "*No output captured.*"
                : content;
        }
        catch (Exception ex)
        {
            MarkdownViewer.Markdown = $"# Error\n\nError loading log: {ex.Message}";
        }
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
            FileName = $"Result_{JobNameText.Text}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(sfd.FileName, MarkdownViewer.Markdown);
                MessageBox.Show("Result exported successfully.", "AgentLoop",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
