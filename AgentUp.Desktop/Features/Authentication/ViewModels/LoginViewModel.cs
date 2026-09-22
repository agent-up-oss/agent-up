using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Authentication.ViewModels;

public sealed class LoginViewModel : ReactiveObject
{
    private readonly AuthenticationController _authentication;
    private readonly Subject<string> _serverSwitched = new();
    private TaskCompletionSource<string?>? _signIn;
    private TaskCompletionSource<bool>? _retryConnection;
    private bool _isVisible;
    private bool _hasConnected;
    private string _password = "";
    private string _serverUrl = "";
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isConnectionRetry;
    private bool _isSwitcher;
    private bool _needsPassword;
    private string? _accessToken;
    private string _openedUrl = "";
    private bool _resumeRequired;
    private readonly Subject<Unit> _sessionRestored = new();

    public LoginViewModel(AuthenticationController authentication)
    {
        _authentication = authentication;
        ServerUrl = authentication.CurrentServerUrl();
        RefreshSavedServers();
        var canSignIn = this.WhenAnyValue(x => x.Password, x => x.IsBusy, x => x.NeedsPassword,
            (password, busy, needsPassword) => !busy && needsPassword && !string.IsNullOrWhiteSpace(password));
        var canConnect = this.WhenAnyValue(x => x.ServerUrl, x => x.IsBusy,
            (url, busy) => !busy && !string.IsNullOrWhiteSpace(url));
        SignInCommand = ReactiveCommand.CreateFromTask(SignInAsync, canSignIn);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);
        SelectSavedCommand = ReactiveCommand.CreateFromTask<string>(SelectSavedAsync, canConnect);
        RemoveSavedCommand = ReactiveCommand.Create<string>(RemoveSaved);
        RetryConnectionCommand = ReactiveCommand.Create(RetryConnection);
        GoBackCommand = ReactiveCommand.Create(GoBack);
    }

    public ObservableCollection<SavedServerDto> SavedServers { get; } = [];

    public IObservable<string> ServerSwitched => _serverSwitched;

    public IObservable<Unit> SessionRestored => _sessionRestored;

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsSwitcher
    {
        get => _isSwitcher;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSwitcher, value);
            this.RaisePropertyChanged(nameof(CanGoBack));
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(Subtitle));
        }
    }

    public bool CanGoBack => IsSwitcher && _hasConnected;

    public string Title => IsSwitcher ? "Switch server" : "Agent-Up Server";

    public string Subtitle => NeedsPassword
        ? "Enter the administrator password to continue."
        : "Choose a saved server or enter a URL. Switching replaces this window's local workspace and browser state.";

    public string ServerUrl
    {
        get => _serverUrl;
        set => this.RaiseAndSetIfChanged(ref _serverUrl, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public bool IsConnectionRetry
    {
        get => _isConnectionRetry;
        private set => this.RaiseAndSetIfChanged(ref _isConnectionRetry, value);
    }

    public bool NeedsPassword
    {
        get => _needsPassword;
        private set
        {
            this.RaiseAndSetIfChanged(ref _needsPassword, value);
            this.RaisePropertyChanged(nameof(Subtitle));
        }
    }

    public string? AccessToken => _accessToken;

    public string CurrentServerUrl => _authentication.CurrentServerUrl();

    public ReactiveCommand<Unit, Unit> SignInCommand { get; }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

    public ReactiveCommand<string, Unit> SelectSavedCommand { get; }

    public ReactiveCommand<string, Unit> RemoveSavedCommand { get; }

    public ReactiveCommand<Unit, Unit> RetryConnectionCommand { get; }

    public ReactiveCommand<Unit, Unit> GoBackCommand { get; }

    public void Show()
    {
        _signIn = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginPrompt(switcher: false, connectionRetry: false);
        NeedsPassword = true;
    }

    public void ShowSwitcher()
    {
        BeginPrompt(switcher: true, connectionRetry: false);
        ErrorMessage = null;
    }

    public void ShowExpired()
    {
        _signIn = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginPrompt(switcher: false, connectionRetry: false);
        _resumeRequired = true;
        NeedsPassword = true;
        ErrorMessage = "This saved sign-in is no longer valid. Enter the administrator password.";
    }

    public void ShowConnectionFailure(string message)
    {
        Show();
        IsConnectionRetry = true;
        ErrorMessage = message;
        _retryConnection = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task<bool> WaitForConnectionRetryAsync()
        => _retryConnection?.Task ?? Task.FromResult(false);

    public void Cancel()
    {
        if (!IsVisible) return;

        Dismiss();
        _retryConnection?.TrySetResult(false);
        _signIn?.TrySetResult(null);
    }

    public void Dismiss()
    {
        IsVisible = false;
        IsConnectionRetry = false;
        IsSwitcher = false;
        NeedsPassword = false;
        ErrorMessage = null;
    }

    public void RememberConnected()
    {
        _hasConnected = true;
        ServerUrl = _authentication.CurrentServerUrl();
        RefreshSavedServers();
    }

    public Task<string?> WaitForSignInAsync()
        => _signIn?.Task ?? Task.FromResult<string?>(null);

    private void BeginPrompt(bool switcher, bool connectionRetry)
    {
        Password = "";
        ErrorMessage = null;
        IsBusy = false;
        IsConnectionRetry = connectionRetry;
        IsSwitcher = switcher;
        NeedsPassword = false;
        ServerUrl = _authentication.CurrentServerUrl();
        _openedUrl = ServerUrl;
        RefreshSavedServers();
        IsVisible = true;
    }

    private void RetryConnection()
    {
        IsConnectionRetry = false;
        ErrorMessage = null;
        _retryConnection?.TrySetResult(true);
    }

    private void GoBack()
    {
        if (!CanGoBack) return;
        Dismiss();
    }

    private void RemoveSaved(string id)
    {
        var selected = SavedServers.FirstOrDefault(server => server.Id == id);
        if (selected is { CanRemove: false })
            return;

        _authentication.RemoveServer(id);
        RefreshSavedServers();
        if (SavedServers.All(server => server.IsFake))
            ServerUrl = _authentication.CurrentServerUrl();
    }

    private async Task SelectSavedAsync(string id)
    {
        var selected = SavedServers.FirstOrDefault(server => server.Id == id);
        if (selected is null) return;
        ServerUrl = selected.Url;
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        NeedsPassword = false;
        try
        {
            _authentication.PrepareServer(ServerUrl);
            ServerUrl = _authentication.CurrentServerUrl();
            if (!await _authentication.IsRequiredAsync())
            {
                CompleteConnection(ServerUrl, token: null);
                return;
            }

            var saved = SavedServers.FirstOrDefault(server =>
                string.Equals(server.Url, ServerUrl, StringComparison.OrdinalIgnoreCase));
            if (saved is { HasCredential: true })
            {
                _authentication.ActivateServer(saved.Id);
                CompleteConnection(ServerUrl, token: null);
                return;
            }

            NeedsPassword = true;
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = $"Could not reach the server: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignInAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _authentication.PrepareServer(ServerUrl);
            ServerUrl = _authentication.CurrentServerUrl();
            _accessToken = await _authentication.LoginAsync(Password);
            CompleteConnection(ServerUrl, _accessToken);
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = $"Could not reach the server: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CompleteConnection(string url, string? token)
    {
        var switched = _hasConnected
            && !string.Equals(_openedUrl, url, StringComparison.OrdinalIgnoreCase);
        var saved = _authentication.SaveServer(url, token);
        if (token is not null)
            _accessToken = token;
        Password = "";
        NeedsPassword = false;
        RememberConnected();
        IsVisible = false;
        IsSwitcher = false;
        IsConnectionRetry = false;
        ErrorMessage = null;
        _signIn?.TrySetResult(token ?? string.Empty);
        if (switched)
            _serverSwitched.OnNext(saved.Url);
        else if (_resumeRequired)
            _sessionRestored.OnNext(Unit.Default);
        _resumeRequired = false;
    }

    private void RefreshSavedServers()
    {
        SavedServers.Clear();
        foreach (var server in _authentication.ListSavedServers().Servers)
            SavedServers.Add(server);
    }
}
