using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Processes.Controllers;

public sealed class ProcessesController
{
    private readonly IWorkspaceProcessManager _processes;
    private readonly ProcessOutputService _output;

    public ProcessesController(IWorkspaceProcessManager processes, ProcessOutputService output)
    {
        _processes = processes;
        _output = output;
    }

    public async Task LaunchWorkspaceAsync(Workspace workspace)
        => await _processes.LaunchAsync(workspace);

    public async Task LaunchApplicationAsync(Workspace workspace, string applicationName)
        => await _processes.LaunchApplicationAsync(workspace, applicationName);

    public async Task KillWorkspaceAsync(string workspaceId)
        => await _processes.KillAsync(workspaceId);

    public async Task KillApplicationAsync(string workspaceId, string applicationName)
        => await _processes.KillApplicationAsync(workspaceId, applicationName);

    public async Task<IReadOnlyList<string>> GetOutputAsync(string workspaceId, string applicationName)
        => await _output.GetAsync(workspaceId, applicationName);
}
