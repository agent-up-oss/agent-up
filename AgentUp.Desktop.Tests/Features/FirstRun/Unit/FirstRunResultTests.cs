using AgentUp.Desktop.Features.FirstRun.Services;

namespace AgentUp.Desktop.Tests.Features.FirstRun.Unit;

[TestFixture]
public sealed class FirstRunResultTests
{
    [Test]
    public void Check_results_distinguish_success_from_actionable_failure()
    {
        var success = FirstRunCheckResult.Success("Docker is available.");
        var failure = FirstRunCheckResult.Failure("Start Docker first.");

        Assert.That(success, Is.EqualTo(new FirstRunCheckResult(true, "Docker is available.")));
        Assert.That(failure, Is.EqualTo(new FirstRunCheckResult(false, "Start Docker first.")));
    }

    [Test]
    public void Sample_failure_does_not_claim_a_project_directory()
    {
        var failure = FirstRunSampleProjectResult.Failure("creation failed");
        var success = FirstRunSampleProjectResult.Success("created", "/tmp/sample");

        Assert.Multiple(() =>
        {
            Assert.That(failure.IsSuccess, Is.False);
            Assert.That(failure.ProjectDirectory, Is.Null);
            Assert.That(success.IsSuccess, Is.True);
            Assert.That(success.ProjectDirectory, Is.EqualTo("/tmp/sample"));
        });
    }
}
