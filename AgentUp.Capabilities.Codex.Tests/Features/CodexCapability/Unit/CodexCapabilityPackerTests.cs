using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Controllers;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

namespace AgentUp.Capabilities.Codex.Tests.Features.CodexCapability.Unit;

[TestFixture]
public sealed class CodexCapabilityPackerTests
{
    [Test]
    public void Manifest_is_an_agent_kind_package()
    {
        var manifest = new CodexCapabilityPacker().Manifest();
        Assert.That(manifest.Id, Is.EqualTo("codex"));
        Assert.That(manifest.Kind, Is.EqualTo("agent"));
        Assert.That(manifest.Launch!.Command, Is.EqualTo("codex-acp"));
    }

    [Test]
    public void DefaultNix_includes_nodejs_and_package_bin_on_PATH()
    {
        var nix = new CodexCapabilityPacker().DefaultNix();
        Assert.That(nix, Does.Contain("nodejs_22"));
        Assert.That(nix, Does.Contain("toString ./bin"));
    }
}
