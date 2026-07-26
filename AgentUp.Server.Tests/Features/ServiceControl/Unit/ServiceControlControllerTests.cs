using System.Net;
using AgentUp.Server.Features.ServiceControl.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Tests.Features.ServiceControl.Unit;

[TestFixture]
public class ServiceControlControllerTests
{
    [TearDown]
    public void TearDown()
    {
        Environment.ExitCode = 0;
    }

    [Test]
    public async Task Restart_returnsAccepted_andSetsNonZeroExitCode()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var controller = new ServiceControlController(lifetime);

        var result = controller.Restart();

        Assert.That(result, Is.InstanceOf<AcceptedResult>());
        Assert.That(Environment.ExitCode, Is.EqualTo(1));

        await Task.Delay(1000);
        Assert.That(lifetime.StopRequested, Is.True);
    }

    [Test]
    public async Task Shutdown_returnsAccepted_andKeepsZeroExitCode()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var controller = new ServiceControlController(lifetime);

        var result = controller.Shutdown();

        Assert.That(result, Is.InstanceOf<AcceptedResult>());
        Assert.That(Environment.ExitCode, Is.EqualTo(0));

        await Task.Delay(1000);
        Assert.That(lifetime.StopRequested, Is.True);
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => StopRequested = true;
    }
}
