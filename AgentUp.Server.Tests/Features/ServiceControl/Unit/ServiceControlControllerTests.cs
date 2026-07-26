using AgentUp.Server.Features.ServiceControl.Interfaces;
using AgentUp.Server.Features.ServiceControl.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Tests.Features.ServiceControl.Unit;

[TestFixture]
public class ServiceControlControllerTests
{
    [Test]
    public async Task Restart_returnsAccepted_andSetsNonZeroExitCode()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var exitCode = new FakeProcessExitCode();
        var controller = new ServiceControlController(lifetime, exitCode);

        var result = controller.Restart();

        Assert.That(result, Is.InstanceOf<AcceptedResult>());
        Assert.That(exitCode.Code, Is.EqualTo(1));

        await Task.Delay(1000);
        Assert.That(lifetime.StopRequested, Is.True);
    }

    [Test]
    public async Task Shutdown_returnsAccepted_andSetsZeroExitCode()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var exitCode = new FakeProcessExitCode();
        var controller = new ServiceControlController(lifetime, exitCode);

        var result = controller.Shutdown();

        Assert.That(result, Is.InstanceOf<AcceptedResult>());
        Assert.That(exitCode.Code, Is.EqualTo(0));

        await Task.Delay(1000);
        Assert.That(lifetime.StopRequested, Is.True);
    }

    [Test]
    public async Task Shutdown_afterRestart_resetsExitCodeToZero()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var exitCode = new FakeProcessExitCode();
        var controller = new ServiceControlController(lifetime, exitCode);

        controller.Restart();
        Assert.That(exitCode.Code, Is.EqualTo(1));

        controller.Shutdown();
        Assert.That(exitCode.Code, Is.EqualTo(0));

        await Task.Delay(1000);
        Assert.That(lifetime.StopRequested, Is.True);
    }

    private sealed class FakeProcessExitCode : IProcessExitCode
    {
        public int Code { get; private set; }
        public void Set(int code) => Code = code;
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
