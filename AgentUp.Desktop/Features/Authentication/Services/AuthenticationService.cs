using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Features.Authentication.Services;

public sealed class AuthenticationService(AuthenticationApiClient client)
{
    public Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
        => client.IsAuthenticationRequiredAsync(cancellationToken);

    public Task<string> LoginAsync(string password, CancellationToken cancellationToken = default)
        => client.LoginAsync(password, cancellationToken);
}
