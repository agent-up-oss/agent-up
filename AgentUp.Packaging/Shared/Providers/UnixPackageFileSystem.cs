using AgentUp.Packaging.Shared.Interfaces;
namespace AgentUp.Packaging.Shared.Providers;

public abstract class UnixPackageFileSystem : PackageFileSystem, IUnixPackageFileSystem
{
    public void CopyDirectory(string source, string destination)
    {
        var fullSource = PackagePathValidator.RequireFullyQualifiedPath(source, nameof(source));
        var fullDestination = PackagePathValidator.RequireFullyQualifiedPath(destination, nameof(destination));
        if (Directory.Exists(fullDestination))
            Directory.Delete(fullDestination, recursive: true);

        FileSystemDirectoryCopier.Copy(fullSource, fullDestination);
    }

    public void SetExecutable(string path)
    {
        var fullPath = PackagePathValidator.RequireFullyQualifiedPath(path, nameof(path));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(fullPath, File.GetUnixFileMode(fullPath) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
