using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Provider;

[TestFixture]
public sealed class NixCapabilityCommandBuilderTests
{
    [Test]
    public void Wrap_uses_nix_shell_when_default_nix_is_present()
    {
        var wrap = new NixCapabilityCommandBuilder().Wrap(
            "dotnet",
            ["run"],
            new NixEnvironmentSpec("/registry/dotnet/1.0.0/default.nix", "abc123", ["dotnet-sdk_10"]));

        Assert.That(wrap.FileName, Is.EqualTo("nix-shell"));
        Assert.That(wrap.Arguments, Is.EqualTo(new[]
        {
            "/registry/dotnet/1.0.0/default.nix", "--run", "'dotnet' 'run'"
        }));
    }

    [Test]
    public void Wrap_uses_nix_shell_for_pinned_packages()
    {
        var wrap = new NixCapabilityCommandBuilder().Wrap(
            "docker",
            ["info"],
            new NixEnvironmentSpec(null, "abc123", ["docker"]));

        Assert.That(wrap.FileName, Is.EqualTo("nix"));
        Assert.That(wrap.Arguments[1], Is.EqualTo("github:NixOS/nixpkgs/abc123#docker"));
        Assert.That(wrap.Arguments, Does.Contain("--command"));
    }
}
