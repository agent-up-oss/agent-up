namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public interface ISymbolicLinkPackageFileSystem : IUnixPackageFileSystem
{
    void CreateSymbolicLink(string linkPath, string targetPath);
}
