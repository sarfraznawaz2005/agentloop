using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AgentLoop.UI.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    private bool _isLoading;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Indicates if the ViewModel is performing a loading operation.
    /// Can be used to show loading indicators in the UI.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        protected set => SetProperty(ref _isLoading, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Minimizes UI churn by only adding/removing items that have actually changed.
    /// </summary>
    protected static void UpdateCollection<T>(System.Collections.ObjectModel.ObservableCollection<T> collection, System.Collections.Generic.IEnumerable<T> newItems, System.Func<T, T, bool> equals)
    {
        var newItemsList = newItems.ToList();

        // Remove items no longer present
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (!newItemsList.Any(ni => equals(ni, collection[i])))
            {
                collection.RemoveAt(i);
            }
        }

        // Add or move items
        for (int i = 0; i < newItemsList.Count; i++)
        {
            var newItem = newItemsList[i];
            var existingIndex = -1;
            for (int j = 0; j < collection.Count; j++)
            {
                if (equals(newItem, collection[j]))
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex == -1)
            {
                collection.Insert(i, newItem);
            }
            else if (existingIndex != i)
            {
                collection.Move(existingIndex, i);
                collection[i] = newItem;
            }
            else
            {
                collection[i] = newItem;
            }
        }
    }
}
