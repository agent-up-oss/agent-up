using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class ReviewSnapshotCommand(WorkspaceCommandService service, WorkspaceCommandOutputService output)
{
    public async Task<int> RunAsync()
        => output.WriteTextResult(await service.GetCurrentReviewSnapshotAsync());
}
