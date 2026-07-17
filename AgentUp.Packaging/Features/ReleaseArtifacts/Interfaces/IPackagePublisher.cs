namespace AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;

public interface IPackagePublisher
{
    Task PublishDotNetProjectAsync(
        string projectPath,
        string runtimeId,
        string configuration,
        string version,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    void CopyPrebuiltPayload(string payloadDirectory, string outputDirectory);
}
