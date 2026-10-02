using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Authentication.ViewModels;

public sealed class LoginViewModel : ReactiveObject
{
    private readonly AuthenticationController _authentication;
    private readonly Subject<string> _serverSwitched = new();
    private TaskCompletionSource<string?>? _signIn;
    private bool _isVisible;
    private bool _hasConnected;
    private string _password = "";
    private string _serverUrl = "";
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isSwitcher;
    private bool _needsPassword;
    private bool _needsBrowserSso;
    private bool _needsIssuedCredential;
    private string _connectionPrompt = "";
    private string? _accessToken;
    private string _openedUrl = "";
    private bool _resumeRequired;
    private ClientSurfaceAvailability _surfaces = ClientSurfaceAvailability.Real;
    private readonly Subject<Unit> _sessionRestored = new();

    public LoginViewModel(AuthenticationController authentication)
    {
        _authentication = authentication;
        ServerUrl = authentication.CurrentServerUrl();
        RefreshSavedServers();
        RefreshSurfaces();
        var canSignIn = this.WhenAnyValue(x => x.Password, x => x.IsBusy, x => x.NeedsPassword,
            (password, busy, needsPassword) => !busy && needsPassword && !string.IsNullOrWhiteSpace(password));
        var canConnect = this.WhenAnyValue(x => x.ServerUrl, x => x.IsBusy,
            (url, busy) => !busy && !string.IsNullOrWhiteSpace(url));
        SignInCommand = ReactiveCommand.CreateFromTask(SignInAsync, canSignIn);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);
        SelectSavedCommand = ReactiveCommand.CreateFromTask<string>(SelectSavedAsync, canConnect);
        RemoveSavedCommand = ReactiveCommand.Create<string>(RemoveSaved);
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

    public string Subtitle => NeedsPassword || NeedsBrowserSso || NeedsIssuedCredential
        ? (string.IsNullOrWhiteSpace(_connectionPrompt)
            ? "Enter the administrator password to continue."
            : _connectionPrompt)
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

    public ClientSurfaceAvailability Surfaces
    {
        get => _surfaces;
        private set => this.RaiseAndSetIfChanged(ref _surfaces, value);
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

    public bool NeedsBrowserSso
    {
        get => _needsBrowserSso;
        private set
        {
            this.RaiseAndSetIfChanged(ref _needsBrowserSso, value);
            this.RaisePropertyChanged(nameof(Subtitle));
        }
    }

    public bool NeedsIssuedCredential
    {
        get => _needsIssuedCredential;
        private set
        {
            this.RaiseAndSetIfChanged(ref _needsIssuedCredential, value);
            this.RaisePropertyChanged(nameof(Subtitle));
        }
    }

    public string? AccessToken => _accessToken;

    public string CurrentServerUrl => _authentication.CurrentServerUrl();

    public string CurrentConnectionId => _authentication.CurrentConnectionId();

    public ReactiveCommand<Unit, Unit> SignInCommand { get; }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

    public ReactiveCommand<string, Unit> SelectSavedCommand { get; }

    public ReactiveCommand<string, Unit> RemoveSavedCommand { get; }

    public ReactiveCommand<Unit, Unit> GoBackCommand { get; }

    public void Show()
    {
        _signIn = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginPrompt(switcher: false);
        NeedsPassword = true;
        _connectionPrompt = "Enter the administrator password to continue.";
        this.RaisePropertyChanged(nameof(Subtitle));
    }

    public void ShowPicker()
    {
        _signIn = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginPrompt(switcher: false);
    }

    public void ShowSwitcher()
    {
        BeginPrompt(switcher: true);
        ErrorMessage = null;
    }

    public async Task ShowExpiredAsync()
    {
        _signIn = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginPrompt(switcher: false);
        _resumeRequired = true;
        await ConnectAsync();
        if (IsVisible && string.IsNullOrWhiteSpace(ErrorMessage))
            ErrorMessage = "This saved sign-in is no longer valid.";
    }

    public void ShowConnectionFailure(string message)
    {
        _signIn ??= new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginPrompt(switcher: false);
        ErrorMessage = message;
    }

    public void Cancel()
    {
        if (!IsVisible) return;

        Dismiss();
        _signIn?.TrySetResult(null);
    }

    public void Dismiss()
    {
        IsVisible = false;
        IsSwitcher = false;
        ClearSignInSurface();
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

    private void BeginPrompt(bool switcher)
    {
        Password = "";
        ErrorMessage = null;
        IsBusy = false;
        IsSwitcher = switcher;
        ClearSignInSurface();
        ServerUrl = _authentication.CurrentServerUrl();
        _openedUrl = ServerUrl;
        RefreshSavedServers();
        RefreshSurfaces();
        IsVisible = true;
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
        ClearSignInSurface();
        try
        {
            _authentication.PrepareServer(ServerUrl);
            ServerUrl = _authentication.CurrentServerUrl();
            var connection = await _authentication.ResolveConnectionAsync();
            ApplySignInSurface(connection);

            var saved = SavedServers.FirstOrDefault(server =>
                string.Equals(server.Url, ServerUrl, StringComparison.OrdinalIgnoreCase));
            if (!_resumeRequired
                && saved is { HasCredential: true }
                && ConnectionSourceParser.SignInSurface(connection.AuthMode) != ConnectionSignInSurface.None)
            {
                _authentication.ActivateServer(saved.Id);
                CompleteConnection(ServerUrl, token: null);
                return;
            }

            if (ConnectionSourceParser.SignInSurface(connection.AuthMode) == ConnectionSignInSurface.None)
            {
                if (_resumeRequired)
                    return;
                CompleteConnection(ServerUrl, token: null);
                return;
            }
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = $"Could not reach the server: {exception.Message}";
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
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
        ClearSignInSurface();
        RememberConnected();
        IsVisible = false;
        IsSwitcher = false;
        ErrorMessage = null;
        _signIn?.TrySetResult(token ?? string.Empty);
        if (switched)
            _serverSwitched.OnNext(saved.Url);
        else if (_resumeRequired)
            _sessionRestored.OnNext(Unit.Default);
        _resumeRequired = false;
    }

    private void ApplySignInSurface(ConnectionSource connection)
    {
        _connectionPrompt = connection.Prompt;
        var surface = ConnectionSourceParser.SignInSurface(connection.AuthMode);
        NeedsPassword = surface == ConnectionSignInSurface.Password;
        NeedsBrowserSso = surface == ConnectionSignInSurface.BrowserSso;
        NeedsIssuedCredential = surface == ConnectionSignInSurface.ExternalBearer;
        this.RaisePropertyChanged(nameof(Subtitle));
    }

    private void ClearSignInSurface()
    {
        NeedsPassword = false;
        NeedsBrowserSso = false;
        NeedsIssuedCredential = false;
        _connectionPrompt = "";
        this.RaisePropertyChanged(nameof(Subtitle));
    }

    private void RefreshSavedServers()
    {
        SavedServers.Clear();
        foreach (var server in _authentication.ListSavedServers().Servers)
            SavedServers.Add(server);
        RefreshSurfaces();
    }

    private void RefreshSurfaces()
        => Surfaces = _authentication.ClientSurfaces();
}
