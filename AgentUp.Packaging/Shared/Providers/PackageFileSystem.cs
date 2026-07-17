using AgentUp.Packaging.Shared.Interfaces;
namespace AgentUp.Packaging.Shared.Providers;

public abstract class PackageFileSystem : IPackageFileSystem
{
    public void ResetDirectory(string path)
    {
        var fullPath = PackagePathValidator.RequireFullyQualifiedPath(path, nameof(path));
        if (Directory.Exists(fullPath))
            Directory.Delete(fullPath, recursive: true);

        Directory.CreateDirectory(fullPath);
    }

    public void CreateDirectory(string path)
        => Directory.CreateDirectory(PackagePathValidator.RequireFullyQualifiedPath(path, nameof(path)));

    public void CopyFile(string source, string destination)
    {
        var fullSource = PackagePathValidator.RequireFullyQualifiedPath(source, nameof(source));
        var fullDestination = PackagePathValidator.RequireFullyQualifiedPath(destination, nameof(destination));
        Directory.CreateDirectory(PackagePathValidator.GetParentDirectory(fullDestination, nameof(destination)));
        File.Copy(fullSource, fullDestination, overwrite: true);
    }

    public void WriteText(string path, string text)
    {
        var fullPath = PackagePathValidator.RequireFullyQualifiedPath(path, nameof(path));
        Directory.CreateDirectory(PackagePathValidator.GetParentDirectory(fullPath, nameof(path)));
        File.WriteAllText(fullPath, text);
    }
}
