using AgentUp.Server.Features.Validation.DTOs;

namespace AgentUp.Server.Tests.Features.Validation.Unit;

public sealed class ValidationRunReportTests
{
    [Test]
    public void Report_recordsAPassedStepWithResolutionAndScreenshot()
    {
        var started = DateTimeOffset.UnixEpoch;
        var finished = started.AddSeconds(2);
        var step = new ValidationStepArtifact(
            "save",
            "Try and save the Server URL",
            nameof(ValidationAction.Click),
            new ValidationTarget(Role: "button", Name: "Try and save"),
            new ValidationTargetResolution("button", "server-connect", "Try and save", 12, 40, 160, 36),
            [new ValidationExpectationResult(ValidationExpectation.Url, "/workspace/abc", true, "/workspace/abc")],
            true,
            180,
            "01-save.png");

        var report = new ValidationRunReport("connect", nameof(ValidationSurface.Mobile), true, started, finished, [step]);

        Assert.Multiple(() =>
        {
            Assert.That(report.Passed, Is.True);
            Assert.That(report.Steps[0].Resolved!.AutomationId, Is.EqualTo("server-connect"));
            Assert.That(report.Steps[0].Screenshot, Is.EqualTo("01-save.png"));
            Assert.That(report.Error, Is.Null);
        });
    }

    [Test]
    public void Report_keepsCandidatesWhenATargetMisses()
    {
        var step = new ValidationStepArtifact(
            "git",
            "Open Git",
            nameof(ValidationAction.Click),
            new ValidationTarget(Role: "tab", Name: "Git"),
            null,
            [new ValidationExpectationResult(ValidationExpectation.Visible, "true", false, Message: "Git tab was not visible.")],
            false,
            40,
            "01-git.png",
            ["Apps", "Agents", "Settings"]);

        var report = new ValidationRunReport(
            "git-changes",
            nameof(ValidationSurface.Mobile),
            false,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMilliseconds(40),
            [step],
            "Git tab was not visible.");

        Assert.Multiple(() =>
        {
            Assert.That(report.Passed, Is.False);
            Assert.That(report.Steps[0].Candidates, Is.EqualTo(new[] { "Apps", "Agents", "Settings" }));
            Assert.That(report.Error, Does.Contain("Git tab"));
        });
    }
}
