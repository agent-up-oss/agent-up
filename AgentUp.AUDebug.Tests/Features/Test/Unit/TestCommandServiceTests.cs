using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Test.Providers;
using AgentUp.AUDebug.Features.Test.Services;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Test.Unit;

[TestFixture]
public sealed class TestCommandServiceTests
{
    [Test]
    public async Task UnknownSuite_doesNotRunProcesses()
    {
        var runner = new FakeTestProcessRunner();
        var result = await Service(runner).RunAsync(Command("packaging"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("unknown test suite"));
            Assert.That(runner.Ran, Is.Empty);
        });
    }

    [Test]
    public async Task ScopedSuite_runsOnlyThatSuite()
    {
        var runner = new FakeTestProcessRunner();
        var result = await Service(runner).RunAsync(Command("design-system"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Message, Does.Contain("design-system"));
            Assert.That(result.Message, Does.Not.Contain("desktop"));
            Assert.That(runner.Ran, Has.Count.EqualTo(2));
            Assert.That(runner.Ran.Select(step => step.WorkingDirectory), Is.All.EqualTo("AgentUp.DesignSystem"));
        });
    }

    [Test]
    public async Task Failure_stopsBeforeLaterSuites()
    {
        var runner = new FakeTestProcessRunner { NextExitCode = 1, NextOutput = "boom" };
        using var output = new StringWriter();
        var result = await Service(runner, output).RunAsync(Command("all"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("design-system"));
            Assert.That(output.ToString(), Does.Contain("boom"));
            Assert.That(runner.Ran, Has.Count.EqualTo(1));
        });
    }

    private static TestCommandService Service(FakeTestProcessRunner runner, TextWriter? output = null)
        => new(new DebugTestSuiteCatalog(), runner, output ?? TextWriter.Null);

    private static DebugCommandDto Command(string suite)
        => new("test", null, null, null, null, TimeSpan.FromSeconds(30), false, suite);
}
