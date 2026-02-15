using AgentLoop.UI.ViewModels;

namespace AgentLoop.UI.Helpers;

public class NavigationService
{
    private readonly Func<Type, ViewModelBase> _viewModelFactory;

    public NavigationService(Func<Type, ViewModelBase> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    public event Action<ViewModelBase>? CurrentViewModelChanged;

    public ViewModelBase? CurrentViewModel { get; private set; }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var viewModel = _viewModelFactory(typeof(TViewModel));
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke(viewModel);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke(viewModel);
    }
}
