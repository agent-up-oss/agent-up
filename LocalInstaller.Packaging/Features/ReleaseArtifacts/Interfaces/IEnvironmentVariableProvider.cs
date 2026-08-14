namespace LocalInstaller.Packaging.Features.ReleaseArtifacts.Interfaces;

public interface IEnvironmentVariableProvider
{
    string? Get(string name);
}
