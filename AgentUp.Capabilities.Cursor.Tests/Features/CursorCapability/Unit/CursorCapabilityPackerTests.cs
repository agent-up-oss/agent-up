using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Unit;

[TestFixture]
public sealed class CursorCapabilityPackerTests
{
    [Test]
    public void Manifest_is_an_agent_kind_package()
    {
        var manifest = new CursorCapabilityPacker().Manifest();
        Assert.That(manifest.Id, Is.EqualTo("cursor"));
        Assert.That(manifest.Launch!.Arguments, Is.EqualTo(new[] { "acp" }));
        Assert.That(manifest.Kind, Is.EqualTo("agent"));
    }

    [Test]
    public void DefaultNix_includes_nodejs()
    {
        Assert.That(new CursorCapabilityPacker().DefaultNix(), Does.Contain("nodejs_22"));
    }
}
