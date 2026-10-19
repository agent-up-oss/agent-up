using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class AuCheckArgumentParserTests
{
    [Test]
    public void Parse_defaultsRepoToTheWorkingDirectoryAndConfigToAgentUpJson()
    {
        var invocation = new AuCheckArgumentParser().Parse(["plan"], "/work/repo");

        Assert.Multiple(() =>
        {
            Assert.That(invocation.Command, Is.EqualTo("plan"));
            Assert.That(invocation.RepositoryRoot, Is.EqualTo(Path.GetFullPath("/work/repo")));
            Assert.That(invocation.ConfigPath, Is.EqualTo("agent-up.json"));
            Assert.That(invocation.ConfigExplicit, Is.False);
            Assert.That(invocation.Error, Is.Null);
        });
    }

    [Test]
    public void Parse_acceptsRepoConfigReportAndACheckId()
    {
        var invocation = new AuCheckArgumentParser().Parse(
            ["--repo", "/elsewhere", "--config", "rules.json", "run", "architecture", "--report", "out"],
            "/work/repo");

        Assert.Multiple(() =>
        {
            Assert.That(invocation.Command, Is.EqualTo("run"));
            Assert.That(invocation.CheckId, Is.EqualTo("architecture"));
            Assert.That(invocation.RepositoryRoot, Is.EqualTo(Path.GetFullPath("/elsewhere")));
            Assert.That(invocation.ConfigPath, Is.EqualTo("rules.json"));
            Assert.That(invocation.ConfigExplicit, Is.True);
            Assert.That(invocation.ReportDirectory, Is.EqualTo("out"));
        });
    }

    [Test]
    public void Parse_rejectsAnUnknownToolchainStyleOptionAsInvalidConfiguration()
    {
        var invocation = new AuCheckArgumentParser().Parse(["plan", "--checks", "unit"], "/work");

        Assert.That(invocation.Error, Does.Contain("Unknown option"));
    }

    [Test]
    public void Parse_acceptsHelpRunMissingAndACoverageFloor()
    {
        var parser = new AuCheckArgumentParser();
        var help = parser.Parse(["--help"], "/work");
        var named = parser.Parse(["help"], "/work");
        var bare = parser.Parse([], "/work");
        var coverage = parser.Parse(["coverage", "--min", "90.5", "--run"], "/work");

        Assert.Multiple(() =>
        {
            Assert.That(help.Command, Is.EqualTo("help"));
            Assert.That(named.Command, Is.EqualTo("help"));
            Assert.That(bare.Command, Is.EqualTo("help"));
            Assert.That(coverage.Command, Is.EqualTo("coverage"));
            Assert.That(coverage.RunMissing, Is.True);
            Assert.That(coverage.Minimum, Is.EqualTo(90.5d));
        });
    }

    [Test]
    public void Parse_rejectsAMissingFlagValueAnUnreadableMinimumAndExtraArguments()
    {
        var parser = new AuCheckArgumentParser();

        Assert.Multiple(() =>
        {
            Assert.That(parser.Parse(["--repo"], "/work").Error, Does.Contain("Missing value"));
            Assert.That(parser.Parse(["-h"], "/work").Command, Is.EqualTo("help"));
            Assert.That(parser.Parse(["coverage", "--min", "lots"], "/work").Error, Does.Contain("not a number"));
            Assert.That(parser.Parse(["run", "unit", "again"], "/work").Error, Does.Contain("Unexpected argument"));
            Assert.That(parser.Parse(["deploy"], "/work").Error, Does.Contain("Unknown command"));
            Assert.That(parser.Parse(["plan", "unit"], "/work").Error, Does.Contain("does not take a check id"));
        });
    }
}
