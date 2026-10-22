using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Models;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class ApplicationReadinessTests
{
    [Test]
    public void Signal_prefersADeclaredHealthCheckOverTcp()
    {
        var signal = ApplicationReadinessSignal.For(App(HealthPort(), ApiPort()));

        Assert.Multiple(() =>
        {
            Assert.That(signal.Kind, Is.EqualTo(ApplicationReadinessKind.HealthCheck));
            Assert.That(signal.Description, Is.EqualTo("http://127.0.0.1:10300/health"));
            Assert.That(signal.TimeoutMessage(TimeSpan.FromSeconds(120)),
                Is.EqualTo("http://127.0.0.1:10300/health never succeeded within 120s"));
        });
    }

    [Test]
    public void Signal_usesTheAllocatedPortWhenNoHealthCheckIsDeclared()
    {
        var signal = ApplicationReadinessSignal.For(App(TcpPort()));

        Assert.Multiple(() =>
        {
            Assert.That(signal.Kind, Is.EqualTo(ApplicationReadinessKind.TcpPort));
            Assert.That(signal.Description, Is.EqualTo("port 10301"));
            Assert.That(signal.TimeoutMessage(TimeSpan.FromSeconds(120)),
                Is.EqualTo("port 10301 never accepted a connection within 120s"));
        });
    }

    [Test]
    public void Signal_usesContainerThenProcessWhenNoPortsAreDeclared()
    {
        var process = ApplicationReadinessSignal.For(new ApplicationInstance { Name = "Editor" });
        var docker = ApplicationReadinessSignal.For(new ApplicationInstance
        {
            Name = "Database",
            ServiceType = ServiceType.Docker
        });

        Assert.Multiple(() =>
        {
            Assert.That(process.Kind, Is.EqualTo(ApplicationReadinessKind.Process));
            Assert.That(docker.Kind, Is.EqualTo(ApplicationReadinessKind.Container));
            Assert.That(ApplicationReadinessDescription.For(processApp()), Is.EqualTo("process"));
        });

        static ApplicationInstance processApp() => new() { Name = "Editor" };
    }

    [Test]
    public void IsReady_requiresTheMatchingProbeAndALiveProcess()
    {
        var port = new ApplicationReadinessSignal(ApplicationReadinessKind.TcpPort, 10301);
        var settle = new ApplicationReadinessSignal(ApplicationReadinessKind.Process);

        Assert.Multiple(() =>
        {
            Assert.That(ApplicationReadiness.IsReady(port, false, true, false, true), Is.False);
            Assert.That(ApplicationReadiness.IsReady(port, true, true, false, false), Is.True);
            Assert.That(ApplicationReadiness.IsReady(settle, true, false, false, false), Is.False);
            Assert.That(ApplicationReadiness.IsReady(settle, true, false, false, true), Is.True);
        });
    }

    [Test]
    public void DiagnosticsSignal_prefersTheFailureMessage()
    {
        var app = App(TcpPort());
        app.LastError = "port 10301 never accepted a connection within 120s";

        Assert.That(ApplicationReadinessDescription.For(app),
            Is.EqualTo("port 10301 never accepted a connection within 120s"));
    }

    [Test]
    public void StateFrom_staysStartingWhileAnyApplicationIsStarting()
    {
        Assert.That(
            WorkspaceStartEvaluation.StateFrom(
            [
                new ApplicationInstance { Name = "Web", State = ApplicationState.Starting },
                new ApplicationInstance { Name = "Api", State = ApplicationState.Failed }
            ]),
            Is.EqualTo(WorkspaceState.Starting));
    }

    private static ApplicationInstance App(params PortMapping[] ports)
        => new()
        {
            Name = "Web",
            AllocatedPorts = ports
        };

    private static PortMapping HealthPort() => new("WEB_PORT", 3000, 10300, "http", "/health");

    private static PortMapping ApiPort() => new("API_PORT", 3001, 10301);

    private static PortMapping TcpPort() => new("WEB_PORT", 3000, 10301);
}
