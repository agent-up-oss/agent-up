using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts.Controller;

[TestFixture]
public class PackageCommandParserTests
{
    [Test]
    public void Parse_withMinimalPackageCommandUsesDefaults()
    {
        var parser = new PackageCommandParser(new RecordingEnvironment(name => name == "AGENTUP_PACKAGE_PAYLOAD_ROOT" ? "/payload" : null));

        var result = parser.Parse(["package", "ubuntu", "linux-x64", "1.2.3"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Command!.Platform, Is.EqualTo("ubuntu"));
        Assert.That(result.Command.RuntimeId, Is.EqualTo("linux-x64"));
        Assert.That(result.Command.Version, Is.EqualTo("1.2.3"));
        Assert.That(result.Command.OutputDirectory, Is.EqualTo("artifacts"));
        Assert.That(result.Command.PayloadRoot, Is.EqualTo("/payload"));
    }

    [Test]
    public void Parse_withPayloadRootArgumentOverridesEnvironment()
    {
        var parser = new PackageCommandParser(new RecordingEnvironment(_ => "/env-payload"));

        var result = parser.Parse(["package", "windows", "win-x64", "2.0.0", "dist", "--payload-root", "/arg-payload"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Command!.OutputDirectory, Is.EqualTo("dist"));
        Assert.That(result.Command.PayloadRoot, Is.EqualTo("/arg-payload"));
    }

    [Test]
    public void Parse_withInvalidShapeReturnsUsage()
    {
        var parser = new PackageCommandParser(new RecordingEnvironment(_ => null));

        var result = parser.Parse(["package", "ubuntu"]);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo(PackageCommandParser.Usage));
    }

    private sealed class RecordingEnvironment : IEnvironmentVariableProvider
    {
        private readonly Func<string, string?> _get;

        public RecordingEnvironment(Func<string, string?> get)
        {
            _get = get;
        }

        public string? Get(string name) => _get(name);
    }
}
