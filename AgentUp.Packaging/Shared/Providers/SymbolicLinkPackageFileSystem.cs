using AgentUp.Packaging.Shared.Interfaces;
namespace AgentUp.Packaging.Shared.Providers;

public abstract class SymbolicLinkPackageFileSystem : UnixPackageFileSystem, ISymbolicLinkPackageFileSystem
{
    public void CreateSymbolicLink(string linkPath, string targetPath)
    {
        var fullLinkPath = PackagePathValidator.RequireFullyQualifiedPath(linkPath, nameof(linkPath));
        var fullTargetPath = PackagePathValidator.RequireFullyQualifiedPath(targetPath, nameof(targetPath));
        Directory.CreateDirectory(PackagePathValidator.GetParentDirectory(fullLinkPath, nameof(linkPath)));
        if (File.Exists(fullLinkPath) || Directory.Exists(fullLinkPath))
            File.Delete(fullLinkPath);

        File.CreateSymbolicLink(fullLinkPath, fullTargetPath);
    }
}
