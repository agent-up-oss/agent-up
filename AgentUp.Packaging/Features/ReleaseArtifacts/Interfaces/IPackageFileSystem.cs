namespace AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;

public interface IPackageFileSystem
{
    void ResetDirectory(string path);
    void CreateDirectory(string path);
    void CopyFile(string source, string destination);
    void WriteText(string path, string text);
}
