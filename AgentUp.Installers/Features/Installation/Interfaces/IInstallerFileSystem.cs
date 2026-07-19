namespace AgentUp.Installers.Features.Installation.Interfaces;

public interface IInstallerFileSystem
{
    void ResetDirectory(string path);
    void DeleteDirectory(string path);
    void DeleteFile(string path);
    void CreateDirectory(string path);
    void CopyDirectory(string source, string destination);
    void WriteText(string path, string text);
    bool FileExists(string path);
}
