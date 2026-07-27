using AgentUp.CLI.Features.Commits.DTOs;

namespace AgentUp.CLI.Features.Commits.Interfaces;

public interface ICommitsFormatParser
{
    (CommitsOutputFormat Format, string? Error) Parse(string[] args);
}
