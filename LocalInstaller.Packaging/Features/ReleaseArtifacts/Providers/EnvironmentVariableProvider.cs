using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

public sealed class EnvironmentVariableProvider : IEnvironmentVariableProvider
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
