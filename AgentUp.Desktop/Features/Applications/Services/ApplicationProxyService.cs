using AgentUp.Desktop.Features.Applications.Providers;

namespace AgentUp.Desktop.Features.Applications.Services;

public sealed class ApplicationProxyService(ApplicationProxyClient proxy)
{
    public Task<Uri> IssueNavigationUriAsync(
        Uri serverUri,
        string workspaceId,
        int allocatedPort,
        CancellationToken cancellationToken = default)
        => proxy.IssueNavigationUriAsync(serverUri, workspaceId, allocatedPort, cancellationToken);
}
