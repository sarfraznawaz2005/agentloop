using System.Windows.Input;
using AgentLoop.UI.Helpers;

namespace AgentLoop.UI.ViewModels;

public class RunPromptViewModel : ViewModelBase
{
    private string _prompt = string.Empty;

    public string Prompt
    {
        get => _prompt;
        set => SetProperty(ref _prompt, value);
    }

    public bool CanRun => !string.IsNullOrWhiteSpace(Prompt);

    // Commands
    public ICommand RunCommand { get; }
    public ICommand ClearCommand { get; }

    public event Action<string, string>? RequestRunPrompt;

    public RunPromptViewModel()
    {
        RunCommand = new RelayCommand(_ => OnRun(), _ => CanRun);
        ClearCommand = new RelayCommand(_ => OnClear());
    }

    private void OnRun()
    {
        if (!CanRun) return;

        RequestRunPrompt?.Invoke("Adhoc Job", Prompt);
    }

    private void OnClear()
    {
        Prompt = string.Empty;
    }
}
