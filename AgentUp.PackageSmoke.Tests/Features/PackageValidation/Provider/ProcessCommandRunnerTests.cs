using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.PackageValidation.Provider;

[TestFixture]
public class ProcessCommandRunnerTests
{
    [Test]
    public async Task RunAsync_rejectsUnknownCommandNames()
    {
        var result = await new ProcessCommandRunner().RunAsync(new CommandSpec("agent-up-command-that-does-not-exist", []));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("not allowed"));
    }

    [Test]
    public async Task RunAsync_rejectsRelativeExecutablePaths()
    {
        var result = await new ProcessCommandRunner().RunAsync(new CommandSpec("tools/agent-up", []));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("paths are not allowed"));
    }

    [Test]
    public async Task RunAsync_rejectsUnsafeEnvironmentKeys()
    {
        var result = await new ProcessCommandRunner().RunAsync(
            new CommandSpec("git", [], Environment: new Dictionary<string, string>
            {
                ["BAD-KEY"] = "value"
            }));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("Environment variable name"));
    }

    [Test]
    public async Task RunAsync_rejectsNonAbsoluteWorkingDirectory()
    {
        var result = await new ProcessCommandRunner().RunAsync(
            new CommandSpec("git", [], "relative-workdir"));

        Assert.That(result.ExitCode, Is.EqualTo(126));
        Assert.That(result.Stderr, Does.Contain("working directory"));
    }
}
