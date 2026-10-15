using System.Reactive;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceCloneViewModel : ReactiveObject
{
    private readonly Func<string, string, bool, Task<bool>> _onConfirm;
    private bool _isVisible;
    private bool _isBusy;
    private bool _derived;
    private string _repository = string.Empty;
    private string _branch = "main";
    private string? _errorMessage;

    public WorkspaceCloneViewModel(Func<string, string, bool, Task<bool>> onConfirm)
    {
        _onConfirm = onConfirm;

        var canConfirm = this.WhenAnyValue(
            x => x.Repository,
            x => x.Branch,
            x => x.IsBusy,
            (repository, branch, busy) =>
                !busy && !string.IsNullOrWhiteSpace(repository) && !string.IsNullOrWhiteSpace(branch));

        ConfirmCommand = ReactiveCommand.CreateFromTask(ConfirmAsync, canConfirm);
        CancelCommand = ReactiveCommand.Create(Hide);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(ConfirmLabel));
        }
    }

    public string ConfirmLabel => _isBusy ? "Cloning…" : Derived ? "Start review" : "Clone";

    public string RevisionLabel => Derived ? "Revision" : "Branch";

    public bool Derived
    {
        get => _derived;
        set
        {
            this.RaiseAndSetIfChanged(ref _derived, value);
            this.RaisePropertyChanged(nameof(ConfirmLabel));
            this.RaisePropertyChanged(nameof(RevisionLabel));
        }
    }

    public string Repository
    {
        get => _repository;
        set => this.RaiseAndSetIfChanged(ref _repository, value);
    }

    public string Branch
    {
        get => _branch;
        set => this.RaiseAndSetIfChanged(ref _branch, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        internal set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    internal void Show()
    {
        Repository = string.Empty;
        Branch = "main";
        Derived = false;
        ErrorMessage = null;
        IsBusy = false;
        IsVisible = true;
    }

    internal void Hide()
    {
        IsVisible = false;
        IsBusy = false;
        Repository = string.Empty;
        Branch = "main";
        Derived = false;
        ErrorMessage = null;
    }

    private async Task ConfirmAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (await _onConfirm(Repository.Trim(), Branch.Trim(), Derived))
                Hide();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
