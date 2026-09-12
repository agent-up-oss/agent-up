using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Test.Controllers;
using AgentUp.AUDebug.Features.Test.Providers;
using AgentUp.AUDebug.Features.Test.Services;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Test.Controller;

[TestFixture]
public sealed class TestControllerTests
{
    [Test]
    public async Task Run_unknownSuite_fails()
    {
        var result = await Controller().RunAsync(
            new DebugCommandDto("test", null, null, null, null, TimeSpan.FromSeconds(30), false, "nope"),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown test suite"));
    }

    [Test]
    public async Task Run_scopedSuite_succeeds()
    {
        var result = await Controller().RunAsync(
            new DebugCommandDto("test", null, null, null, null, TimeSpan.FromSeconds(30), false, "au-debug"),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Message, Does.Contain("au-debug"));
    }

    private static TestController Controller()
        => new(new TestCommandService(new DebugTestSuiteCatalog(), new FakeTestProcessRunner(), TextWriter.Null));
}
