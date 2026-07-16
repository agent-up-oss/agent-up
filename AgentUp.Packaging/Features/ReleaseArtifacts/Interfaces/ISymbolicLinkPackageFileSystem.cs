namespace AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;

public interface ISymbolicLinkPackageFileSystem : IUnixPackageFileSystem
{
    void CreateSymbolicLink(string linkPath, string targetPath);
}
