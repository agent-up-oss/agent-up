using AgentUp.CLI.Features.Capabilities.Providers;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilitiesArgParserTests
{
    [Test]
    public void Parse_readsAnEnableId()
    {
        var (command, error) = new CapabilitiesArgParser().Parse(["enable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(command!.Verb, Is.EqualTo("enable"));
            Assert.That(command.Id, Is.EqualTo(CliDomain.CapabilityId));
        });
    }

    // Capabilities are built into the Server, so there is no package version left to choose.
    [Test]
    public void Parse_rejectsTheRetiredVersionFlag()
    {
        var (command, error) = new CapabilitiesArgParser().Parse(["enable", CliDomain.CapabilityId, "--version", "1.0.0"]);

        Assert.That(command, Is.Null);
        Assert.That(error, Does.Contain("--version"));
    }

    [Test]
    public void Parse_requiresAnIdToEnable()
    {
        var (command, error) = new CapabilitiesArgParser().Parse(["enable"]);

        Assert.Multiple(() =>
        {
            Assert.That(command, Is.Null);
            Assert.That(error, Does.Contain("enable <id>"));
        });
    }

    [Test]
    public void Parse_readsADisableId()
    {
        var (command, error) = new CapabilitiesArgParser().Parse(["disable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(command!.Verb, Is.EqualTo("disable"));
            Assert.That(command.Id, Is.EqualTo(CliDomain.CapabilityId));
        });
    }

    [Test]
    public void Parse_rejectsUnknownAndExtraEnableArguments()
    {
        var parser = new CapabilitiesArgParser();

        Assert.Multiple(() =>
        {
            Assert.That(parser.Parse(["enable", CliDomain.CapabilityId, "--flag"]).Error, Does.Contain("Unknown argument"));
            Assert.That(parser.Parse(["enable", CliDomain.CapabilityId, "other"]).Error, Does.Contain("enable <id>"));
        });
    }

    [Test]
    public void Parse_readsListAndBootstrapWithoutAnId()
    {
        var parser = new CapabilitiesArgParser();

        Assert.Multiple(() =>
        {
            Assert.That(parser.Parse(["list"]).Command!.Verb, Is.EqualTo("list"));
            Assert.That(parser.Parse(["bootstrap"]).Command!.Verb, Is.EqualTo("bootstrap"));
        });
    }
}
