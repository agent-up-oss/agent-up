using AgentUp.CLI.Features.Authentication.Services;

namespace AgentUp.CLI.Features.Authentication.Controllers;

public sealed class AuthLogoutCommand(AuthenticationService service, TextWriter output)
{
    public Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        service.Logout();
        output.WriteLine("Logged out.");
        return Task.FromResult(0);
    }
}
