using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Services;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Unit;

[TestFixture]
public sealed class NixCapabilityEnvironmentServiceTests
{
    [Test]
    public void Wrap_returns_the_builder_command()
    {
        var wrap = new NixCapabilityEnvironmentService(new NixCapabilityCommandBuilder(), new CapabilityIndexMergeProvider())
            .Wrap("node", ["--version"], new NixEnvironmentSpec(null, "rev", ["nodejs_22"]));

        Assert.That(wrap.FileName, Is.EqualTo("nix"));
        Assert.That(wrap.Arguments[0], Is.EqualTo("shell"));
    }

    [Test]
    public void Wrap_leaves_commands_unchanged_without_packages()
    {
        var wrap = new NixCapabilityEnvironmentService(new NixCapabilityCommandBuilder(), new CapabilityIndexMergeProvider())
            .Wrap("printenv", [], new NixEnvironmentSpec(null, null, []));

        Assert.That(wrap.FileName, Is.EqualTo("printenv"));
        Assert.That(wrap.Arguments, Is.Empty);
    }
}
