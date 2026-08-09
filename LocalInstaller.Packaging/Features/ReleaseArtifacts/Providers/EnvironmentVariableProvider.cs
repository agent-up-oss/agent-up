using LocalInstaller.Packaging.Features.ReleaseArtifacts.Interfaces;

namespace LocalInstaller.Packaging.Features.ReleaseArtifacts.Providers;

public sealed class EnvironmentVariableProvider : IEnvironmentVariableProvider
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
