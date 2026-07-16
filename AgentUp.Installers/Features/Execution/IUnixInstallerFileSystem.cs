namespace AgentUp.Installers.Features.Execution;

public interface IUnixInstallerFileSystem : IInstallerFileSystem
{
    void CopyFile(string source, string destination);
    void CreateSymbolicLink(string path, string target);
    void SetExecutable(string path);
}
