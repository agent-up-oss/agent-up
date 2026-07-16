namespace AgentUp.Installers.Features.Execution.Providers;

public interface IInstallerFileSystem
{
    void ResetDirectory(string path);
    void CreateDirectory(string path);
    void CopyDirectory(string source, string destination);
    void WriteText(string path, string text);
    bool FileExists(string path);
}
