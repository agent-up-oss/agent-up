using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Ports.Services;
using AgentUp.Desktop.Features.Ports.ViewModels;

namespace AgentUp.Desktop.Features.Ports.Controllers;

public sealed class PortsController
{
    private readonly PortTabService _service;

    public PortsController(PortTabService service)
    {
        _service = service;
    }

    public IReadOnlyList<SubTabViewModel> CreateTabs(ApplicationViewModel application)
        => _service.CreateTabs(application);
}
