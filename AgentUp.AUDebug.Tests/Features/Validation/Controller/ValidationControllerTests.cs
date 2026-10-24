using AgentUp.AUDebug.Features.Validation.Controllers;
using AgentUp.AUDebug.Features.Validation.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Validation.Controller;

[TestFixture]
public sealed class ValidationControllerTests
{
    [Test]
    public async Task RunAsync_replaysDesktopSurfaceFlows()
    {
        var launch = new FakeDesktopValidationLaunch();
        var controller = new ValidationController(new ValidationCommandService(launch, new FakeEnvironment()));

        var result = await controller.RunAsync(
            DebugDomain.Validate().WritingTo("/tmp/out").Build(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(launch.Ran, Has.Count.EqualTo(1));
            Assert.That(launch.Ran[0].OutputPath, Is.EqualTo("/tmp/out"));
        });
    }

    [Test]
    public async Task RunAsync_returnsTheFailedDesktopExit()
    {
        var launch = new FakeDesktopValidationLaunch
        {
            NextResult = new(1, "", "flow failed")
        };
        var controller = new ValidationController(new ValidationCommandService(launch, new FakeEnvironment()));

        var result = await controller.RunAsync(DebugDomain.Validate().Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("flow failed"));
    }
}
