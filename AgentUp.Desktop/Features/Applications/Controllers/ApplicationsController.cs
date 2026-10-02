using AgentUp.Desktop.Features.Applications.Providers;
using AgentUp.Desktop.Features.Applications.Services;
using AgentUp.Desktop.Features.Applications.ViewModels;

namespace AgentUp.Desktop.Features.Applications.Controllers;

public sealed class ApplicationsController
{
    private readonly ApplicationSelectionService _service;
    private readonly ApplicationProxyClient? _proxy;

    public ApplicationsController(ApplicationSelectionService service, ApplicationProxyClient? proxy = null)
    {
        _service = service;
        _proxy = proxy;
    }

    public IReadOnlyList<ApplicationViewModel> Normalize(IReadOnlyList<ApplicationViewModel> applications)
        => _service.Normalize(applications);

    public Task<Uri> IssueProxyNavigationAsync(
        Uri serverUri,
        string workspaceId,
        int allocatedPort,
        CancellationToken cancellationToken = default)
        => (_proxy ?? throw new InvalidOperationException("Application proxy navigation is not configured."))
            .IssueNavigationUriAsync(serverUri, workspaceId, allocatedPort, cancellationToken);
}
