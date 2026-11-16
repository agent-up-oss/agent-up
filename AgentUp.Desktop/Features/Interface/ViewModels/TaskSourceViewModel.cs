using System.Diagnostics;
using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// The repository this Server's agents take work from.
///
/// Naming it and signing in are two steps because they are two decisions: which repository, and
/// then letting this Server read it. The sign-in leaves the app for the host's own authorize page,
/// which is the only place a password or a passkey should ever be typed; the Server is called back
/// directly by the host, so this surface re-reads rather than waiting on anything.
/// </summary>
public sealed class TaskSourceViewModel : ReactiveObject
{
    private readonly TaskSourceController _source;
    private readonly Func<string, bool> _open;
    private TaskSource? _state;
    private string _typed = string.Empty;
    private bool _editing;
    private string? _error;

    public TaskSourceViewModel(TaskSourceController source, Func<string, bool>? open = null)
    {
        _source = source;
        _open = open ?? OpenInBrowser;
        EditCommand = ReactiveCommand.Create(Edit);
        CancelCommand = ReactiveCommand.Create(Cancel);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        ForgetCommand = ReactiveCommand.CreateFromTask(ForgetAsync);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask(() => LoadAsync());
    }

    public ReactiveCommand<Unit, Unit> EditCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> ForgetCommand { get; }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public string Label => _state?.Label ?? "Not set";

    public string Detail => _error ?? _state?.Detail ?? "Reading the task source.";

    public bool HasError => _error is { Length: > 0 };

    public bool Connected => _state?.Connected == true;

    public bool Configured => _state?.Configured == true;

    /// <summary>Shown only when a sign-in could actually be started.</summary>
    public bool ShowsConnect => Configured && !Connected && _state?.CanConnect == true;

    public bool Editing
    {
        get => _editing;
        private set
        {
            this.RaiseAndSetIfChanged(ref _editing, value);
            this.RaisePropertyChanged(nameof(Reading));
        }
    }

    public bool Reading => !Editing;

    public string EditLabel => Configured ? "Change" : "Set repository";

    public string Typed
    {
        get => _typed;
        set
        {
            this.RaiseAndSetIfChanged(ref _typed, value);
            this.RaisePropertyChanged(nameof(CanSave));
        }
    }

    /// <summary>A half-named source is not saved: it would leave the Server pointed at nothing.</summary>
    public bool CanSave => TaskSourceService.Split(Typed) is not null;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _state = await _source.ReadAsync(cancellationToken);
            _error = null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
        }

        Raise();
    }

    private void Edit()
    {
        Typed = _state?.Configured == true ? _state.Label : string.Empty;
        Editing = true;
    }

    private void Cancel()
    {
        Typed = string.Empty;
        Editing = false;
    }

    private async Task SaveAsync()
    {
        var split = TaskSourceService.Split(Typed);
        if (split is null) return;

        // The host comes from what the Server already has, or from the one module it offers.
        var moduleId = _state?.ModuleId ?? _state?.Modules.FirstOrDefault()?.ModuleId ?? "github";
        await ChangeAsync(token => _source.SetAsync(moduleId, split.Value.Owner, split.Value.Repository, token));
        Editing = false;
        Typed = string.Empty;
    }

    private Task ForgetAsync() => ChangeAsync(_source.ClearAsync);

    private async Task ConnectAsync()
    {
        try
        {
            var url = await _source.StartLoginAsync();
            if (url is null)
            {
                _error = "This Server could not start a sign-in for that host.";
                Raise();
                return;
            }

            // A machine with no browser to hand it to is worth saying out loud: the reader would
            // otherwise press Connect and watch nothing happen.
            _error = _open(url) ? null : $"Could not open a browser. Sign in at {url}";
            Raise();
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
            Raise();
        }
    }

    private async Task ChangeAsync(Func<CancellationToken, Task<TaskSource?>> change)
    {
        try
        {
            _state = await change(CancellationToken.None);
            _error = null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            _error = failure.Message;
        }

        Raise();
    }

    private void Raise()
    {
        this.RaisePropertyChanged(nameof(Label));
        this.RaisePropertyChanged(nameof(Detail));
        this.RaisePropertyChanged(nameof(HasError));
        this.RaisePropertyChanged(nameof(Connected));
        this.RaisePropertyChanged(nameof(Configured));
        this.RaisePropertyChanged(nameof(ShowsConnect));
        this.RaisePropertyChanged(nameof(EditLabel));
    }

    /// <summary>
    /// Hands the authorize page to the machine's own browser. The reader signs in where their
    /// session and passkeys already are, rather than in a window this app controls.
    /// </summary>
    private static bool OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
