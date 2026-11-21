using AgentUp.CLI.Features.Capabilities.Services;

namespace AgentUp.CLI.Features.Capabilities.Controllers;

public sealed class CapabilitiesListCommand(CapabilitiesService service, CapabilitiesOutputService output)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => RunServiceAsync(service, output, args, cancellationToken);

    internal static async Task<int> RunServiceAsync(
        CapabilitiesService service,
        CapabilitiesOutputService output,
        string[] args,
        CancellationToken cancellationToken)
        => output.Write(await service.RunAsync(args, cancellationToken));
}
