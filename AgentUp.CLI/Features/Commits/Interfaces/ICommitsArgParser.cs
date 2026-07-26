using AgentUp.CLI.Features.Commits.DTOs;

namespace AgentUp.CLI.Features.Commits.Interfaces;

public interface ICommitsArgParser
{
    (EnqueueRequest? Request, string? Error) Parse(string[] args);
}
