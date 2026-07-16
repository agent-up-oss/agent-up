namespace AgentUp.Packaging.Features.Ubuntu;

public sealed class FileSystemPackageWriter : IPackageWriter
{
    public void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);

        Directory.CreateDirectory(path);
    }

    public void CreateDirectory(string path)
        => Directory.CreateDirectory(path);

    public void CopyDirectory(string source, string destination)
    {
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        CopyDirectoryRecursive(new DirectoryInfo(source), new DirectoryInfo(destination));
    }

    public void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    public void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void CreateSymbolicLink(string linkPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        if (File.Exists(linkPath) || Directory.Exists(linkPath))
            File.Delete(linkPath);

        File.CreateSymbolicLink(linkPath, targetPath);
    }

    public void SetExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    private static void CopyDirectoryRecursive(DirectoryInfo source, DirectoryInfo destination)
    {
        Directory.CreateDirectory(destination.FullName);
        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Join(destination.FullName, file.Name), overwrite: true);

        foreach (var child in source.GetDirectories())
            CopyDirectoryRecursive(child, new DirectoryInfo(Path.Join(destination.FullName, child.Name)));
    }
}
