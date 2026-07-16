namespace AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

public interface IUnixPackageFileSystem : IPackageFileSystem
{
    void CopyDirectory(string source, string destination);
    void SetExecutable(string path);
}
