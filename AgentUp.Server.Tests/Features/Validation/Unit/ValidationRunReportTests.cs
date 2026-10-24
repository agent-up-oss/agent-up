using AgentUp.Server.Features.Validation.DTOs;

namespace AgentUp.Server.Tests.Features.Validation.Unit;

public sealed class ValidationRunReportTests
{
    [Test]
    public void Report_recordsAPassedStepWithResolutionAndScreenshot()
    {
        var started = DateTimeOffset.UnixEpoch;
        var finished = started.AddSeconds(2);
        var target = new ValidationTarget(Role: "button", Name: "Try and save");
        var resolved = new ValidationTargetResolution("button", "server-connect", "Try and save", 12, 40, 160, 36);
        var expectation = new ValidationExpectationResult(ValidationExpectation.Url, "/workspace/abc", true, "/workspace/abc");
        var step = new ValidationStepArtifact(
            "save",
            "Try and save the Server URL",
            nameof(ValidationAction.Click),
            target,
            resolved,
            [expectation],
            true,
            180,
            "01-save.png");

        var report = new ValidationRunReport("connect", nameof(ValidationSurface.Mobile), true, started, finished, [step]);

        Assert.Multiple(() =>
        {
            Assert.That(report.FlowId, Is.EqualTo("connect"));
            Assert.That(report.Surface, Is.EqualTo(nameof(ValidationSurface.Mobile)));
            Assert.That(report.Passed, Is.True);
            Assert.That(report.StartedAtUtc, Is.EqualTo(started));
            Assert.That(report.FinishedAtUtc, Is.EqualTo(finished));
            Assert.That(report.Error, Is.Null);
            Assert.That(report.Steps[0].Screenshot, Is.EqualTo("01-save.png"));
            Assert.That(report.Steps[0].Resolved!.AutomationId, Is.EqualTo("server-connect"));
            Assert.That((resolved.ControlType, resolved.Y, resolved.Height), Is.EqualTo(("button", 40d, 36d)));
            Assert.That(expectation.Kind, Is.EqualTo(ValidationExpectation.Url));
        });
    }

    [Test]
    public void Report_exposesEveryStepArtifactField()
    {
        var target = new ValidationTarget(Role: "tab", Name: "Git");
        var resolved = new ValidationTargetResolution("tab", null, "Git", 8, 790, 90, 48);
        var expectation = new ValidationExpectationResult(ValidationExpectation.Visible, "true", false, null, "Git tab was not visible.");
        var step = new ValidationStepArtifact(
            "git",
            "Open Git",
            nameof(ValidationAction.Click),
            target,
            resolved,
            [expectation],
            false,
            40,
            "01-git.png",
            ["Apps", "Agents", "Settings"]);

        Assert.Multiple(() =>
        {
            Assert.That(step.Id, Is.EqualTo("git"));
            Assert.That(step.Description, Is.EqualTo("Open Git"));
            Assert.That(step.Action, Is.EqualTo(nameof(ValidationAction.Click)));
            Assert.That(step.Target, Is.SameAs(target));
            Assert.That(step.Resolved, Is.SameAs(resolved));
            Assert.That(step.Expectations, Has.Count.EqualTo(1));
            Assert.That(step.Passed, Is.False);
            Assert.That(step.DurationMs, Is.EqualTo(40));
            Assert.That(step.Screenshot, Is.EqualTo("01-git.png"));
            Assert.That(step.Candidates, Is.EqualTo(new[] { "Apps", "Agents", "Settings" }));
        });
    }

    [Test]
    public void Report_keepsCandidatesWhenATargetMisses()
    {
        var resolved = new ValidationTargetResolution("tab", null, "Git", 8, 790, 90, 48);
        var expectation = new ValidationExpectationResult(ValidationExpectation.Visible, "true", false, Message: "Git tab was not visible.");
        var step = new ValidationStepArtifact(
            "git",
            "Open Git",
            nameof(ValidationAction.Click),
            null,
            null,
            [expectation],
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
            Assert.That(expectation.Passed, Is.False);
            Assert.That(expectation.Value, Is.EqualTo("true"));
            Assert.That(expectation.Actual, Is.Null);
            Assert.That(expectation.Message, Does.Contain("Git tab"));
            Assert.That(resolved.Name, Is.EqualTo("Git"));
            Assert.That(resolved.X, Is.EqualTo(8));
            Assert.That(resolved.Width, Is.EqualTo(90));
        });
    }
}
