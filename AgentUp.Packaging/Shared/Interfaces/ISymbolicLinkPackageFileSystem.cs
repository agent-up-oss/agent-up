namespace AgentUp.Packaging.Shared.Interfaces;

public interface ISymbolicLinkPackageFileSystem : IUnixPackageFileSystem
{
    void CreateSymbolicLink(string linkPath, string targetPath);
}
