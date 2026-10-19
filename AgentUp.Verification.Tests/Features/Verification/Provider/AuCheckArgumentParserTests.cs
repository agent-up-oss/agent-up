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
}
