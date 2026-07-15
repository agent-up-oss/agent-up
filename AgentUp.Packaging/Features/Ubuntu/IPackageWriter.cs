namespace AgentUp.Packaging.Features.Ubuntu;

public interface IPackageWriter
{
    void ResetDirectory(string path);
    void CreateDirectory(string path);
    void CopyDirectory(string source, string destination);
    void CopyFile(string source, string destination);
    void WriteText(string path, string text);
    void CreateSymbolicLink(string linkPath, string targetPath);
    void SetExecutable(string path);
}
