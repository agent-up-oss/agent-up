using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Unit;

[TestFixture]
public sealed class FirstPartyNixPinTests
{
    [Test]
    public void DefaultNix_puts_package_bin_and_dev_root_on_PATH()
    {
        var nix = FirstPartyNixPin.DefaultNix(["nodejs_22"]);

        Assert.That(nix, Does.Contain("toString ./bin"));
        Assert.That(nix, Does.Contain("AGENT_UP_DEV_ROOT"));
        Assert.That(nix, Does.Contain("nodejs_22"));
    }
}
