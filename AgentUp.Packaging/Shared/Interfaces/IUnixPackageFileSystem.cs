namespace AgentUp.Packaging.Shared.Interfaces;

public interface IUnixPackageFileSystem : IPackageFileSystem
{
    void CopyDirectory(string source, string destination);
    void SetExecutable(string path);
}
