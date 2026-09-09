using AgentUp.CLI.Features.Authentication.Models;
using AgentUp.CLI.Features.Authentication.Providers;

namespace AgentUp.CLI.Features.Authentication.Services;

public sealed class AuthenticationCommandService(
    AuthenticationService authentication,
    AuthenticationArgParser parser)
{
    public async Task<AuthenticationCommandResult> LoginAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await authentication.IsAuthenticationRequiredAsync(cancellationToken))
                return AuthenticationCommandResult.Success("Authentication is not required for this server.");

            var password = parser.ParsePassword(args);
            if (string.IsNullOrEmpty(password))
                return AuthenticationCommandResult.Failure("Error: Admin password is required. Pass --password or run interactively.");

            await authentication.LoginAsync(password, cancellationToken);
            return AuthenticationCommandResult.Success("Authenticated.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return AuthenticationCommandResult.Failure($"Error: {ex.Message}");
        }
    }

    public async Task<AuthenticationCommandResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var required = await authentication.IsAuthenticationRequiredAsync(cancellationToken);
            if (!required)
                return AuthenticationCommandResult.Success("Authentication is not required for this server.");

            return authentication.HasStoredToken()
                ? AuthenticationCommandResult.Success("Authenticated.")
                : AuthenticationCommandResult.Failure(AuthenticationMessages.LoginHint);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return AuthenticationCommandResult.Failure($"Error: {ex.Message}");
        }
    }
}
