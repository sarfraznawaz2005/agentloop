using System.Windows;
using System.Windows.Controls;

namespace AgentLoop.UI.Helpers;

public static class GridViewColumnStretch
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(GridViewColumnStretch),
            new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty StretchColumnProperty =
        DependencyProperty.RegisterAttached(
            "StretchColumn",
            typeof(bool),
            typeof(GridViewColumnStretch),
            new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);
    public static bool GetStretchColumn(DependencyObject obj) => (bool)obj.GetValue(StretchColumnProperty);
    public static void SetStretchColumn(DependencyObject obj, bool value) => obj.SetValue(StretchColumnProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView && (bool)e.NewValue)
        {
            listView.Loaded += ListView_Loaded;
            listView.SizeChanged += ListView_SizeChanged;

            // Disable horizontal scrollbar as we are stretching columns to fit the available space
            ScrollViewer.SetHorizontalScrollBarVisibility(listView, ScrollBarVisibility.Disabled);
        }
    }

    private static void ListView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListView listView)
            UpdateColumnWidths(listView);
    }

    private static void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateColumnWidths(sender as ListView);
    }

    private static void UpdateColumnWidths(ListView? listView)
    {
        if (listView == null) return;

        var view = listView.View as GridView;
        if (view == null || view.Columns.Count == 0) return;

        double totalWidth = listView.ActualWidth;
        if (totalWidth <= 0) return;

        double fixedWidth = 0;
        int stretchableCount = 0;

        foreach (var column in view.Columns)
        {
            if (GetStretchColumn(column))
                stretchableCount++;
            else
            {
                if (!double.IsNaN(column.Width))
                    fixedWidth += column.Width;
            }
        }

        if (stretchableCount == 0) return;

        // Buffer for vertical scrollbar and borders (approx 25-30 pixels)
        double availableWidth = totalWidth - fixedWidth - 35;
        if (availableWidth <= 0) return;

        double newWidth = availableWidth / stretchableCount;
        foreach (var column in view.Columns)
        {
            if (GetStretchColumn(column))
                column.Width = newWidth;
        }
    }
}
