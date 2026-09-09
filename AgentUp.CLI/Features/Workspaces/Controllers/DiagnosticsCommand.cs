using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class DiagnosticsCommand(WorkspaceCommandService service, WorkspaceCommandOutputService output)
{
    public async Task<int> RunAsync()
        => output.WriteDiagnosticsResult(await service.GetCurrentDiagnosticsAsync());
}
