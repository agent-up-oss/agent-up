using Avalonia.Controls;
using Avalonia.Layout;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Features.Authentication.Views;

public sealed class LoginWindow : Window
{
    private readonly AuthenticationApiClient _authentication;
    private readonly TextBox _password = new() { PasswordChar = '●', PlaceholderText = "Admin password" };
    private readonly TextBlock _error = new() { Foreground = Avalonia.Media.Brushes.OrangeRed };
    private readonly Button _submit = new() { Content = "Sign in", HorizontalAlignment = HorizontalAlignment.Stretch };

    public LoginWindow(AuthenticationApiClient authentication)
    {
        _authentication = authentication;
        Title = "Sign in to Agent-Up";
        Width = 420;
        Height = 250;
        CanResize = false;
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(32),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "Agent-Up Server", FontSize = 25, FontWeight = Avalonia.Media.FontWeight.Bold },
                new TextBlock { Text = "Enter the administrator password to continue." },
                _password,
                _submit,
                _error,
            }
        };
        _submit.Click += async (_, _) => await SubmitAsync();
    }

    public string? AccessToken { get; private set; }

    private async Task SubmitAsync()
    {
        _submit.IsEnabled = false;
        _error.Text = string.Empty;
        try
        {
            AccessToken = await _authentication.LoginAsync(_password.Text ?? string.Empty);
            Close();
        }
        catch (HttpRequestException exception)
        {
            _error.Text = $"Could not reach the server: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            _error.Text = exception.Message;
        }
        finally
        {
            _submit.IsEnabled = true;
        }
    }
}
