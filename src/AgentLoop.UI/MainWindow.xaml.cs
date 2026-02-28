using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AgentLoop.UI.Helpers;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = (MainViewModel)DataContext;

        _viewModel.RequestAddJob += OnRequestAddJob;
        _viewModel.RequestEditJob += OnRequestEditJob;
        _viewModel.RequestViewJob += OnRequestViewJob;
        _viewModel.RequestRunNow += OnRequestRunNow;
        _viewModel.RequestOpenSettings += OnRequestOpenSettings;
        _viewModel.RequestOpenRunPrompt += OnRequestOpenRunPrompt;
        _viewModel.RequestViewLog += OnRequestViewLog;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }

    private void OnRequestAddJob()
    {
        var dialog = new Views.AddEditJobDialog { Owner = this };
        if (dialog.ShowDialog() == true)
            _ = _viewModel.RefreshAsync();
    }

    private void OnRequestEditJob(Data.Models.JobModel job)
    {
        var dialog = new Views.AddEditJobDialog(job) { Owner = this };
        if (dialog.ShowDialog() == true)
            _ = _viewModel.RefreshAsync();
    }

    private void OnRequestViewJob(Data.Models.JobModel job)
    {
        var dialog = new Views.ViewJobDialog(job) { Owner = this };
        dialog.ShowDialog();
    }

    private void OnRequestRunNow(Data.Models.JobModel job)
    {
        _viewModel.MarkJobRunning(job.Name, true);
        var dialog = new Views.RunJobDialog(job) { Owner = this };
        dialog.ShowDialog();
        _viewModel.MarkJobRunning(job.Name, false);
    }

    private void OnRequestOpenSettings()
    {
        var dialog = new Views.SettingsDialog { Owner = this };
        dialog.ShowDialog();
    }

    private void OnRequestOpenRunPrompt()
    {
        var dialog = new Views.RunPromptDialog { Owner = this };
        dialog.ShowDialog();
        //_ = _viewModel.RefreshAsync(); we don't need to refresh the view model here but we keep this code for reference.
    }

    private void OnRequestViewLog(Data.Models.LogEntry log)
    {
        var dialog = new Views.LogViewerDialog(log) { Owner = this };
        dialog.ShowDialog();
    }

    private void AboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.AboutDialog { Owner = this };
        dialog.ShowDialog();
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
        {
            var listView = sender as ListView;
            if (listView?.ItemsSource is System.Collections.ICollection view)
            {
                var binding = header.Column.DisplayMemberBinding as System.Windows.Data.Binding;
                var sortBy = binding?.Path?.Path ?? GridViewColumnSort.GetSortPropertyName(header.Column);

                if (!string.IsNullOrEmpty(sortBy) && sortBy != "Actions")
                {
                    var viewSource = CollectionViewSource.GetDefaultView(view);
                    var currentSort = viewSource.SortDescriptions.FirstOrDefault();
                    var newDirection = ListSortDirection.Ascending;

                    if (currentSort.PropertyName == sortBy && currentSort.Direction == ListSortDirection.Ascending)
                        newDirection = ListSortDirection.Descending;

                    viewSource.SortDescriptions.Clear();
                    viewSource.SortDescriptions.Add(new SortDescription(sortBy, newDirection));
                }
            }
        }
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void LoadMoreRecentLogs_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (RecentLogsList.Items.Count > 0)
            {
                var lastItem = RecentLogsList.Items[RecentLogsList.Items.Count - 1];
                RecentLogsList.ScrollIntoView(lastItem);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void JobListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedJob != null && _viewModel.ViewJobCommand.CanExecute(null))
        {
            _viewModel.ViewJobCommand.Execute(null);
        }
    }

    private void JobListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (_viewModel.SelectedJob != null && _viewModel.ViewJobCommand.CanExecute(null))
            {
                _viewModel.ViewJobCommand.Execute(null);
            }
        }
    }

    private void RecentLogsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedRecentLog != null && _viewModel.ViewRecentLogCommand.CanExecute(null))
        {
            _viewModel.ViewRecentLogCommand.Execute(null);
        }
    }

    private void RecentLogsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (_viewModel.SelectedRecentLog != null && _viewModel.ViewRecentLogCommand.CanExecute(null))
            {
                _viewModel.ViewRecentLogCommand.Execute(null);
            }
        }
    }
}
