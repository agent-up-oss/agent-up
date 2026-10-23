using AgentUp.CLI.Features.Capabilities.Services;

namespace AgentUp.CLI.Features.Capabilities.Controllers;

public sealed class CapabilitiesEnableCommand(CapabilitiesService service, CapabilitiesOutputService output, string workingDirectory)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => CapabilitiesListCommand.RunServiceAsync(service, output, args, workingDirectory, cancellationToken);
}
