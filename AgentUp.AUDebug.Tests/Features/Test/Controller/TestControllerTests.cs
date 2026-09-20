using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Test.Controllers;
using AgentUp.AUDebug.Features.Test.Providers;
using AgentUp.AUDebug.Features.Test.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Test.Controller;

[TestFixture]
public sealed class TestControllerTests
{
    [Test]
    public async Task Run_unknownSuite_fails()
    {
        var result = await Controller().RunAsync(
            DebugDomain.Verb("test")
                .WithSuite("nope")
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown test suite"));
    }

    [Test]
    public async Task Run_scopedSuite_succeeds()
    {
        var result = await Controller().RunAsync(
            DebugDomain.Verb("test")
                .WithSuite("au-debug")
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Message, Does.Contain("au-debug"));
    }

    [Test]
    public async Task Run_unknownBuildTarget_fails()
    {
        var result = await Controller().RunAsync(
            DebugDomain.Verb("build")
                .WithSuite("desktop")
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown build target"));
    }

    private static TestController Controller()
        => new(new TestCommandService(new DebugTestSuiteCatalog(), new FakeTestProcessRunner(), TextWriter.Null));
}
