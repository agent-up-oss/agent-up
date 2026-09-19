using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.Services;
using AgentUp.Desktop.Features.Ports.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Ports.Unit;

[TestFixture]
public sealed class PortTabServiceTests
{
    [Test]
    public void CreateTabs_maps_declared_ports_then_appends_console_and_metrics()
    {
        var result = new PortTabService().CreateTabs([
            new PortTabRequest("WEB_PORT", 5173, 10100, "http"),
            new PortTabRequest("DB_PORT", 5432, 10101, "tcp")]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(4));
            Assert.That(result[0], Is.TypeOf<PortSubTabViewModel>());
            Assert.That(result[1], Is.TypeOf<PortSubTabViewModel>());
            Assert.That(result[2], Is.TypeOf<ConsoleSubTabViewModel>());
            Assert.That(result[3], Is.TypeOf<MetricsSubTabViewModel>());
        });
    }

    [Test]
    public void CreateTabs_still_exposes_console_and_metrics_without_ports()
        => Assert.That(new PortTabService().CreateTabs([]),
            Is.TypeOf<List<AgentUp.Desktop.Features.Ports.ViewModels.SubTabViewModel>>()
                .And.Count.EqualTo(2));
}
