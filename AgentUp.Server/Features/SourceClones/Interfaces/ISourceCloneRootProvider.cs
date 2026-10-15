namespace AgentUp.Server.Features.SourceClones.Interfaces;

public interface ISourceCloneRootProvider
{
    string GetRoot();

    bool IsUnderRoot(string path);

    void DeleteIfUnderRoot(string path);
}
