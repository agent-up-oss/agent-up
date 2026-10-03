using AgentUp.Desktop.Features.Applications.Services;
using AgentUp.Desktop.Features.Applications.ViewModels;

namespace AgentUp.Desktop.Features.Applications.Controllers;

public sealed class ApplicationsController
{
    private readonly ApplicationSelectionService _service;

    public ApplicationsController(ApplicationSelectionService service)
    {
        _service = service;
    }

    public IReadOnlyList<ApplicationViewModel> Normalize(IReadOnlyList<ApplicationViewModel> applications)
        => _service.Normalize(applications);
}
