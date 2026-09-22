using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Unit;

[TestFixture]
public sealed class ClaudeCapabilityPackerTests
{
    [Test]
    public void Manifest_is_an_agent_kind_package()
    {
        var manifest = new ClaudeCapabilityPacker().Manifest();
        Assert.That(manifest.Id, Is.EqualTo("claude"));
        Assert.That(manifest.Launch!.Command, Is.EqualTo("claude-agent-acp"));
        Assert.That(manifest.Kind, Is.EqualTo("agent"));
    }

    [Test]
    public void DefaultNix_includes_nodejs()
    {
        Assert.That(new ClaudeCapabilityPacker().DefaultNix(), Does.Contain("nodejs_22"));
    }
}
