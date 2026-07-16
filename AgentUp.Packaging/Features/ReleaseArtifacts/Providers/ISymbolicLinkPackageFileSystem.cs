namespace AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

public interface ISymbolicLinkPackageFileSystem : IUnixPackageFileSystem
{
    void CreateSymbolicLink(string linkPath, string targetPath);
}
