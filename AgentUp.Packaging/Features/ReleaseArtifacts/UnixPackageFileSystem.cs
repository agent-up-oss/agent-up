namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public abstract class UnixPackageFileSystem : PackageFileSystem, IUnixPackageFileSystem
{
    public void CopyDirectory(string source, string destination)
    {
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        FileSystemDirectoryCopier.Copy(source, destination);
    }

    public void SetExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
