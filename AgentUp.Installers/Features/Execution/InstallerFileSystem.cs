namespace AgentUp.Installers.Features.Execution;

public abstract class InstallerFileSystem : IInstallerFileSystem
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
            Directory.CreateDirectory(System.IO.Path.Join(destination, System.IO.Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, System.IO.Path.Join(destination, System.IO.Path.GetRelativePath(source, file)), overwrite: true);
    }

    public void WriteText(string path, string text)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public bool FileExists(string path)
        => File.Exists(path);
}
