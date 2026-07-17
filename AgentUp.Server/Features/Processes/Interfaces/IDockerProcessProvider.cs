using System.Diagnostics;
using AgentUp.Server.Features.Applications.DTOs;

namespace AgentUp.Server.Features.Processes.Interfaces;

public interface IDockerProcessProvider
{
    string GetContainerName(string workspaceId, string appName);
    IReadOnlyList<string> CreateRunArguments(string containerName, ApplicationInstance app);
    Process CreateLogProcess(string containerName);
    Task<DockerCommandResult> RunAsync(params string[] args);
    Task<int> GetExitCodeAsync(string containerName);
}

public sealed record DockerCommandResult(int ExitCode, string Stdout, string Stderr);
