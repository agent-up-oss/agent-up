using AgentUp.Desktop.Features.Ports.Controllers;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.Services;
using AgentUp.Desktop.Features.Ports.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Ports.Controller;

[TestFixture]
public sealed class PortsControllerTests
{
    [Test]
    public void CreateTabs_maps_port_details_at_the_controller_boundary()
    {
        var result = new PortsController(new PortTabService()).CreateTabs([
            new PortTabRequest("WEB_PORT", 5173, 12000, "http")]);

        var port = (PortSubTabViewModel)result[0];
        Assert.That((port.Variable, port.DefaultPort, port.AllocatedPort, port.Protocol),
            Is.EqualTo(("WEB_PORT", 5173, 12000, "http")));
    }

    [Test]
    public void CreateTabs_appends_operational_tabs_after_all_declared_ports()
    {
        var result = new PortsController(new PortTabService()).CreateTabs([]);

        Assert.That(result.Select(tab => tab.GetType()),
            Is.EqualTo(new[] { typeof(ConsoleSubTabViewModel), typeof(MetricsSubTabViewModel) }));
    }
}
