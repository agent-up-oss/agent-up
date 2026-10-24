using AgentUp.AUDebug.Features.Validate.DTOs;
using AgentUp.AUDebug.Features.Validate.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Validate.Provider;

[TestFixture]
public sealed class MobileValidationRunnerTests
{
    [Test]
    public async Task Run_invokesTheNodeFlowRunnerUnderMobileE2E()
    {
        var processes = new FakeProcessRunner();
        var paths = new FakePathValidator("/tmp/au-debug-tests");
        var runner = new MobileValidationRunner(processes, paths);

        await runner.RunAsync(new MobileValidationRunDto(null, "/tmp/au-debug-tests/out"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Ran, Has.Count.EqualTo(1));
            Assert.That(processes.Ran[0].FileName, Is.EqualTo("node"));
            Assert.That(processes.Ran[0].WorkingDirectory, Is.EqualTo(Path.Join(paths.RepositoryRoot, "AgentUp.Mobile.E2E")));
            Assert.That(processes.Ran[0].Arguments, Is.EqualTo(new[] { MobileValidationRunner.ScriptPath, "--out", "/tmp/au-debug-tests/out" }));
            Assert.That(processes.Ran[0].Environment!["AGENTUP_VALIDATE_REPO"], Is.EqualTo(paths.RepositoryRoot));
        });
    }

    [Test]
    public async Task Run_passesAFlowIdWhenTheCommandNamesOne()
    {
        var processes = new FakeProcessRunner();
        var runner = new MobileValidationRunner(processes, new FakePathValidator("/tmp/au-debug-tests"));

        await runner.RunAsync(new MobileValidationRunDto("git-review", "/tmp/au-debug-tests/out"), CancellationToken.None);

        Assert.That(processes.Ran[0].Arguments, Does.Contain("git-review"));
        Assert.That(processes.Ran[0].Arguments, Does.Contain("--flow"));
    }
}
