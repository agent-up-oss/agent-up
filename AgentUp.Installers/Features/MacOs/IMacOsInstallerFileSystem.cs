namespace AgentUp.Installers.Features.MacOs;

public interface IMacOsInstallerFileSystem
{
    void ResetDirectory(string path);
    void CreateDirectory(string path);
    void CopyDirectory(string source, string destination);
    void CopyFile(string source, string destination);
    void WriteText(string path, string text);
    void CreateSymbolicLink(string path, string target);
    void SetExecutable(string path);
    bool FileExists(string path);
}
