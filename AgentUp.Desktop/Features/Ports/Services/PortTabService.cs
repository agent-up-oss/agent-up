using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Ports.ViewModels;

namespace AgentUp.Desktop.Features.Ports.Services;

public sealed class PortTabService
{
    public IReadOnlyList<SubTabViewModel> CreateTabs(ApplicationViewModel application)
    {
        var tabs = application.AllocatedPorts
            .Select(port => (SubTabViewModel)new PortSubTabViewModel(
                port.Variable,
                port.DefaultPort,
                port.AllocatedPort,
                port.Protocol))
            .ToList();
        tabs.Add(new ConsoleSubTabViewModel());
        return tabs;
    }
}
