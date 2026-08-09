using AgentUp.Installers.Features.Installation.Interfaces;

namespace AgentUp.Installers.Features.Installation.Models;

public abstract class UnixInstallerFileSystem : InstallerFileSystem, IUnixInstallerFileSystem
{
    public void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
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
}
