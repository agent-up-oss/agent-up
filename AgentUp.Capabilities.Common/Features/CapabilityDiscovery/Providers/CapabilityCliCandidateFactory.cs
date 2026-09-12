namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

public static class CapabilityCliCandidateFactory
{
    public static IReadOnlyList<CapabilityCliCandidate> FromDeclaredCommand(
        string? command,
        IReadOnlyList<string>? arguments,
        IReadOnlyList<string>? versionArguments)
    {
        if (string.IsNullOrWhiteSpace(command))
            return [];

        return
        [
            new(
                command,
                versionArguments is { Count: > 0 } ? versionArguments : ["--version"],
                arguments ?? [])
        ];
    }
}
