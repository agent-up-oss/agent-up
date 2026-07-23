using AgentUp.Server.Features.Processes.Repositories;

namespace AgentUp.Server.Features.Processes.Services;

public sealed class ProcessOutputService
{
    private readonly IOutputRepository _output;

    public ProcessOutputService(IOutputRepository output)
    {
        _output = output;
    }

    public async Task<IReadOnlyList<string>> GetAsync(string workspaceId, string applicationName)
        => await _output.GetAsync(workspaceId, applicationName);
}
