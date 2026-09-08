using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Authentication.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Authentication.ViewModels;

public sealed class LoginViewModel : ReactiveObject
{
    private readonly AuthenticationController _authentication;
    private TaskCompletionSource<string?>? _signIn;
    private TaskCompletionSource<bool>? _retryConnection;
    private bool _isVisible;
    private string _password = "";
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isConnectionRetry;
    private string? _accessToken;

    public LoginViewModel(AuthenticationController authentication)
    {
        _authentication = authentication;
        var canSignIn = this.WhenAnyValue(x => x.Password, x => x.IsBusy,
            (password, busy) => !busy && !string.IsNullOrWhiteSpace(password));
        SignInCommand = ReactiveCommand.CreateFromTask(SignInAsync, canSignIn);
        RetryConnectionCommand = ReactiveCommand.Create(RetryConnection);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
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

    public string? AccessToken => _accessToken;

    public ReactiveCommand<Unit, Unit> SignInCommand { get; }

    public ReactiveCommand<Unit, Unit> RetryConnectionCommand { get; }

    public void Show()
    {
        _signIn = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Password = "";
        ErrorMessage = null;
        IsBusy = false;
        IsConnectionRetry = false;
        IsVisible = true;
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

        IsVisible = false;
        _retryConnection?.TrySetResult(false);
        _signIn?.TrySetResult(null);
    }

    public Task<string?> WaitForSignInAsync()
        => _signIn?.Task ?? Task.FromResult<string?>(null);

    private void RetryConnection()
    {
        IsConnectionRetry = false;
        ErrorMessage = null;
        _retryConnection?.TrySetResult(true);
    }

    private async Task SignInAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _accessToken = await _authentication.LoginAsync(Password);
            IsVisible = false;
            _signIn?.TrySetResult(_accessToken);
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
}
