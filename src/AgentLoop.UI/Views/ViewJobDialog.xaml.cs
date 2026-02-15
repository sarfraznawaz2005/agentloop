using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AgentLoop.Data.Models;
using AgentLoop.UI.Helpers;
using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Views;

public partial class ViewJobDialog : Window
{
    private readonly ViewJobViewModel _viewModel;

    public ViewJobDialog(JobModel job)
    {
        InitializeComponent();
        _viewModel = new ViewJobViewModel(job);
        DataContext = _viewModel;

        _viewModel.RequestBack += () => Close();
        _viewModel.RequestViewLog += OnViewLog;
        _viewModel.RequestRunJob += OnRunJob;
    }

    private void OnViewLog(LogEntry log)
    {
        var dialog = new LogViewerDialog(log) { Owner = this };
        dialog.ShowDialog();
    }

    private void OnRunJob(JobModel job)
    {
        var dialog = new RunJobDialog(job) { Owner = this };
        dialog.ShowDialog();
    }

    private void LoadMore_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (RunHistoryList.Items.Count > 0)
            {
                var lastItem = RunHistoryList.Items[RunHistoryList.Items.Count - 1];
                RunHistoryList.ScrollIntoView(lastItem);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
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

    private void RunHistoryList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedLog != null)
        {
            OnViewLog(_viewModel.SelectedLog);
        }
    }

    private void RunHistoryList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (_viewModel.SelectedLog != null)
            {
                OnViewLog(_viewModel.SelectedLog);
            }
        }
    }
}
