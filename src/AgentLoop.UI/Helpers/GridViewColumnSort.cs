using System.Windows;
using System.Windows.Controls;

namespace AgentLoop.UI.Helpers;

public static class GridViewColumnSort
{
    public static readonly DependencyProperty SortPropertyNameProperty =
        DependencyProperty.RegisterAttached(
            "SortPropertyName",
            typeof(string),
            typeof(GridViewColumnSort),
            new PropertyMetadata(null));

    public static string GetSortPropertyName(DependencyObject obj)
    {
        return (string)obj.GetValue(SortPropertyNameProperty);
    }

    public static void SetSortPropertyName(DependencyObject obj, string value)
    {
        obj.SetValue(SortPropertyNameProperty, value);
    }
}
