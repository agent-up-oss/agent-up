using AgentUp.AUDebug.Features.Validate.Controllers;
using AgentUp.AUDebug.Features.Validate.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Validate.Controller;

[TestFixture]
public sealed class ValidateControllerTests
{
    [Test]
    public async Task Run_mobile_succeeds()
    {
        var result = await Controller().RunAsync(DebugDomain.Validate().Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Message, Does.Contain("Mobile validation passed."));
    }

    [Test]
    public async Task Run_desktop_refusesReplay()
    {
        var result = await Controller().RunAsync(DebugDomain.Validate("desktop").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Desktop surface replay is not implemented."));
    }

    private static ValidateController Controller()
        => new(new ValidateCommandService(new FakeMobileValidationRunner(), new FakePathValidator("/tmp/au-debug-tests")));
}
