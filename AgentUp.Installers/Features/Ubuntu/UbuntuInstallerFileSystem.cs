namespace AgentUp.Installers.Features.Ubuntu;

public sealed class UbuntuInstallerFileSystem : IUbuntuInstallerFileSystem
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
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException(source);

        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(System.IO.Path.Combine(destination, System.IO.Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, System.IO.Path.Combine(destination, System.IO.Path.GetRelativePath(source, file)), overwrite: true);
    }

    public void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    public void WriteText(string path, string text)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void CreateSymbolicLink(string path, string target)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        if (File.Exists(path) || Directory.Exists(path))
            File.Delete(path);
        File.CreateSymbolicLink(path, target);
    }

    public void SetExecutable(string path)
    {
        if (!OperatingSystem.IsWindows() && File.Exists(path))
            File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    public bool FileExists(string path)
        => File.Exists(path);
}
