namespace AgentUp.Packaging.Features.Windows;

public sealed class WindowsFileSystemPackageWriter : IWindowsPackageWriter
{
    public void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);

        Directory.CreateDirectory(path);
    }

    public void CreateDirectory(string path)
        => Directory.CreateDirectory(path);

    public void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
