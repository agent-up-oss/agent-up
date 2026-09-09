using AgentUp.CLI.Features.Authentication.Services;

namespace AgentUp.CLI.Features.Authentication.Controllers;

public sealed class AuthStatusCommand(AuthenticationCommandService service, AuthenticationOutputService output)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        => output.WriteResult(await service.GetStatusAsync(cancellationToken));
}
