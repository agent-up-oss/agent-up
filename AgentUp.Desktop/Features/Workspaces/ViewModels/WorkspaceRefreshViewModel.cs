using System.Reactive;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceRefreshViewModel : ReactiveObject
{
    private readonly Func<string, string, Task<bool>> _onConfirm;
    private string _workspaceId = string.Empty;
    private string _revision = string.Empty;
    private bool _isVisible;
    private bool _isBusy;
    private string? _errorMessage;

    public WorkspaceRefreshViewModel(Func<string, string, Task<bool>> onConfirm)
    {
        _onConfirm = onConfirm;

        var canConfirm = this.WhenAnyValue(
            x => x.Revision,
            x => x.IsBusy,
            (revision, busy) => !busy && !string.IsNullOrWhiteSpace(revision));

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

    public string ConfirmLabel => IsBusy ? "Refreshing…" : "Refresh";

    public string Revision
    {
        get => _revision;
        set => this.RaiseAndSetIfChanged(ref _revision, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        internal set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    internal void Show(string workspaceId, string revision)
    {
        _workspaceId = workspaceId;
        Revision = revision;
        ErrorMessage = null;
        IsBusy = false;
        IsVisible = true;
    }

    internal void Hide()
    {
        IsVisible = false;
        IsBusy = false;
        _workspaceId = string.Empty;
        Revision = string.Empty;
        ErrorMessage = null;
    }

    private async Task ConfirmAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (await _onConfirm(_workspaceId, Revision.Trim()))
                Hide();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
