using AgentUp.Desktop.Features.Applications.Services;

namespace AgentUp.Desktop.Features.Applications.Controllers;

public sealed class ApplicationProxyController(ApplicationProxyService proxy)
{
    public Task<Uri> IssueNavigationAsync(
        Uri serverUri,
        string workspaceId,
        int allocatedPort,
        CancellationToken cancellationToken = default)
        => proxy.IssueNavigationUriAsync(serverUri, workspaceId, allocatedPort, cancellationToken);
}
