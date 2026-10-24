namespace AgentUp.CLI.Features.Capabilities.Providers;

public static class CapabilityRegistryRootResolver
{
    public static string Resolve(string? configured, string repositoryRoot)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured.Trim());

        return Path.GetFullPath(Path.Join(repositoryRoot, ".agent-up-dev", "capability-registry"));
    }
}
