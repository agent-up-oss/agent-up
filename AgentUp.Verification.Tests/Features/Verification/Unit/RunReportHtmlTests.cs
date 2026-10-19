using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class RunReportHtmlTests
{
    [Test]
    public void Full_leadsWithTheVerdictThenCouldNotRunThenFailures()
    {
        var html = RunReportHtml.Full(Report());

        var verdict = html.IndexOf("id=\"verdict\"", StringComparison.Ordinal);
        var couldNot = html.IndexOf("id=\"could-not-run\"", StringComparison.Ordinal);
        var failures = html.IndexOf("id=\"failures\"", StringComparison.Ordinal);
        var passed = html.IndexOf("id=\"passed\"", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(verdict, Is.GreaterThan(0));
            Assert.That(couldNot, Is.GreaterThan(verdict));
            Assert.That(failures, Is.GreaterThan(couldNot));
            Assert.That(passed, Is.GreaterThan(failures));
            Assert.That(html, Does.Contain("could not run 1"));
            Assert.That(html, Does.Contain("docker is not installed"));
            Assert.That(html, Does.Not.Contain("http://"));
            Assert.That(html, Does.Not.Contain("https://"));
        });
    }

    [Test]
    public void Summary_showsTheSkipInTheFirstScreenAndLeavesThePassListOut()
    {
        var html = RunReportHtml.Summary(Report());

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("docker is not installed"));
            Assert.That(html, Does.Contain("id=\"could-not-run\""));
            Assert.That(html, Does.Not.Contain("id=\"passed\""));
            Assert.That(html.IndexOf("could not run", StringComparison.Ordinal),
                Is.LessThan(html.IndexOf("id=\"failures\"", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void Summary_saysEveryCheckCouldRunAndPointsAtTheFullReportForFurtherFailures()
    {
        var html = RunReportHtml.Summary(Report(
            new RunReportCheck("unit", "fast", "dotnet test", "failed", 12, null, null, "boom"),
            new RunReportCheck("lint", "fast", "dotnet format", "failed", 4, null, null, "format-boom")));

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Every required check could run on this machine."));
            Assert.That(html, Does.Contain("1 more failure(s) are in report.html."));
            Assert.That(html, Does.Contain("boom"));
            Assert.That(html, Does.Not.Contain("format-boom"));
        });
    }

    [Test]
    public void Full_printsASkipReasonWhenNoToolchainTextIsAttached()
    {
        var html = RunReportHtml.Full(Report(
            new RunReportCheck("hosted", "platform", "dotnet test", "skipped", null, "platformMismatch", null, null)));

        Assert.That(html, Does.Contain("platformMismatch"));
    }

    private static RunReport Report(params RunReportCheck[] checks)
        => new(
            "0.0.0",
            "/repo/agent-up.json",
            "sha256:abc",
            "abc123",
            ["src/A.cs"],
            checks.Length == 0
                ?
                [
                    new RunReportCheck("image", "fast", "docker info", "skipped", null, "toolchainMissing", "docker is not installed", null),
                    new RunReportCheck("unit", "fast", "dotnet test", "failed", 12, null, null, "boom"),
                    new RunReportCheck("lint", "fast", "dotnet format", "passed", 4, null, null, null)
                ]
                : checks,
            2,
            1,
            1,
            1,
            2,
            "toolchainMissing");
}
