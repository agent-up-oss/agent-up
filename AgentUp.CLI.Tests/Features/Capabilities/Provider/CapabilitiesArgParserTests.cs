using AgentUp.CLI.Features.Capabilities.Providers;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilitiesArgParserTests
{
    [Test]
    public void Parse_readsEnableIdAndVersion()
    {
        var (command, error) = new CapabilitiesArgParser().Parse(
            ["enable", CliDomain.CapabilityId, "--version", CliDomain.CapabilityVersion]);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(command!.Verb, Is.EqualTo("enable"));
            Assert.That(command.Id, Is.EqualTo(CliDomain.CapabilityId));
            Assert.That(command.Version, Is.EqualTo(CliDomain.CapabilityVersion));
        });
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
