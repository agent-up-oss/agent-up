using AgentUp.Desktop.Features.Authentication.Services;

namespace AgentUp.Desktop.Features.Authentication.Controllers;

public sealed class AuthenticationController(AuthenticationService service)
{
    public Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
        => service.IsRequiredAsync(cancellationToken);

    public Task<string> LoginAsync(string password, CancellationToken cancellationToken = default)
        => service.LoginAsync(password, cancellationToken);
}
