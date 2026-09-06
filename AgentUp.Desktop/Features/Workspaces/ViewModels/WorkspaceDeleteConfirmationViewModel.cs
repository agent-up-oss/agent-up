using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceDeleteConfirmationViewModel : ReactiveObject
{
    private readonly Func<string, Task> _onConfirm;
    private readonly Action _onCancel;
    private bool _isVisible;
    private string _workspaceId = string.Empty;
    private string _workspaceName = string.Empty;
    private bool _confirmChecked;

    public WorkspaceDeleteConfirmationViewModel(
        Func<string, Task> onConfirm,
        Action onCancel)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        var canConfirm = this.WhenAnyValue(x => x.ConfirmChecked);
        ConfirmCommand = ReactiveCommand.CreateFromTask(ConfirmAsync, canConfirm);
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public string WorkspaceName
    {
        get => _workspaceName;
        private set
        {
            this.RaiseAndSetIfChanged(ref _workspaceName, value);
            this.RaisePropertyChanged(nameof(Message));
        }
    }

    public string Message =>
        $"Remove \"{WorkspaceName}\" from Agent-Up? This does not delete files on disk.";

    public bool ConfirmChecked
    {
        get => _confirmChecked;
        set => this.RaiseAndSetIfChanged(ref _confirmChecked, value);
    }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    internal void Show(string workspaceId, string workspaceName)
    {
        _workspaceId = workspaceId;
        WorkspaceName = workspaceName;
        ConfirmChecked = false;
        IsVisible = true;
    }

    internal void Hide()
    {
        IsVisible = false;
        _workspaceId = string.Empty;
        WorkspaceName = string.Empty;
        ConfirmChecked = false;
    }

    private async Task ConfirmAsync()
    {
        if (string.IsNullOrWhiteSpace(_workspaceId))
            return;

        await _onConfirm(_workspaceId);
    }

    private void Cancel() => _onCancel();
}
