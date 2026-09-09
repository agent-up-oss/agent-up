using AgentUp.CLI.Features.Authentication.Services;

namespace AgentUp.CLI.Features.Authentication.Controllers;

public sealed class AuthLoginCommand(AuthenticationCommandService service, AuthenticationOutputService output)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => output.WriteResult(await service.LoginAsync(args, cancellationToken));
}
