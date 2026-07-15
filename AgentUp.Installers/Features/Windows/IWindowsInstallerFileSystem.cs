namespace AgentUp.Installers.Features.Windows;

public interface IWindowsInstallerFileSystem
{
    void ResetDirectory(string path);
    void CreateDirectory(string path);
    void CopyDirectory(string source, string destination);
    void WriteText(string path, string text);
    bool FileExists(string path);
}
