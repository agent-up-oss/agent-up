using AgentUp.PackageSmoke.Features.SmokeRuns.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.SmokeRuns.Controller;

[TestFixture]
public sealed class SmokeCommandParserTests
{
    [Test]
    public void Parse_accepts_package_validation_command()
    {
        var result = new SmokeCommandParser().Parse(["validate-package", "ubuntu", "linux-x64", "artifacts", "work"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Request!.Command, Is.EqualTo("validate-package"));
        Assert.That(result.Request.Platform, Is.EqualTo("ubuntu"));
        Assert.That(result.Request.RuntimeId, Is.EqualTo("linux-x64"));
        Assert.That(Path.IsPathFullyQualified(result.Request.ArtifactDirectory), Is.True);
        Assert.That(Path.IsPathFullyQualified(result.Request.WorkDirectory), Is.True);
    }

    [Test]
    public void Parse_accepts_installer_flow_command_with_payload_root()
    {
        var result = new SmokeCommandParser().Parse(["validate-installer-flow", "ubuntu", "work", "payload"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Request!.Command, Is.EqualTo("validate-installer-flow"));
        Assert.That(result.Request.RuntimeId, Is.Empty);
        Assert.That(result.Request.ArtifactDirectory, Is.Empty);
        Assert.That(Path.IsPathFullyQualified(result.Request.PayloadRoot!), Is.True);
    }

    [Test]
    public void Parse_rejects_unknown_command()
    {
        var result = new SmokeCommandParser().Parse(["unknown"]);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Usage, Does.Contain("AgentUp.PackageSmoke"));
    }
}
