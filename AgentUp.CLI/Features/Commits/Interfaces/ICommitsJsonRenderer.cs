namespace AgentUp.CLI.Features.Commits.Interfaces;

public interface ICommitsJsonRenderer
{
    string Serialize<T>(T value);
}
