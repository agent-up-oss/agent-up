using AgentUp.PackageSmoke.Features.Validation;
using AgentUp.PackageSmoke.Features.Validation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.Validation;

[TestFixture]
public class ProcessCommandRunnerTests
{
    [Test]
    public async Task RunAsync_reportsMissingCommandAsFailedResult()
    {
        var result = await new ProcessCommandRunner().RunAsync(new CommandSpec("agent-up-command-that-does-not-exist", []));

        Assert.That(result.ExitCode, Is.EqualTo(127));
        Assert.That(result.Stderr, Is.Not.Empty);
    }
}
