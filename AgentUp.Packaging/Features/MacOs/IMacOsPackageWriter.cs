namespace AgentUp.Packaging.Features.MacOs;

public interface IMacOsPackageWriter
{
    void ResetDirectory(string path);
    void CreateDirectory(string path);
    void CopyDirectory(string source, string destination);
    void CopyFile(string source, string destination);
    void WriteText(string path, string text);
    void SetExecutable(string path);
}
