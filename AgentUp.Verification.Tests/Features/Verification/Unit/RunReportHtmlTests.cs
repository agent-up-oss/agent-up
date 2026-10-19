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

    private static RunReport Report()
        => new(
            "0.0.0",
            "/repo/agent-up.json",
            "sha256:abc",
            "abc123",
            ["src/A.cs"],
            [
                new RunReportCheck("image", "fast", "docker info", "skipped", null, "toolchainMissing", "docker is not installed", null),
                new RunReportCheck("unit", "fast", "dotnet test", "failed", 12, null, null, "boom"),
                new RunReportCheck("lint", "fast", "dotnet format", "passed", 4, null, null, null)
            ],
            2,
            1,
            1,
            1,
            2,
            "toolchainMissing");
}
