using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Unit;

[TestFixture]
public sealed class FirstPartyNixPinTests
{
    [Test]
    public void DefaultNix_puts_package_bin_and_dev_root_on_PATH()
    {
        var nix = FirstPartyNixPin.DefaultNix(["nodejs_22"]);

        Assert.That(nix, Does.Contain("import <nixpkgs>"));
        Assert.That(nix, Does.Contain("toString ./bin"));
        Assert.That(nix, Does.Contain("''${AGENT_UP_DEV_ROOT-}"));
        Assert.That(nix, Does.Contain("nodejs_22"));
        Assert.That(nix, Does.Not.Contain("DOTNET_ROOT"));
    }

    [Test]
    public void DefaultNix_points_dotnet_at_the_sdk_root_instead_of_the_host_testhost()
    {
        var nix = FirstPartyNixPin.DefaultNix(["dotnet-sdk_10"]);

        Assert.That(nix, Does.Contain("DOTNET_ROOT=\"${pkgs.dotnet-sdk_10}/share/dotnet\""));
        Assert.That(nix, Does.Contain("unset MSBuildSDKsPath"));
        Assert.That(nix, Does.Contain("unset DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"));
        Assert.That(nix, Does.Contain("DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1"));
        Assert.That(nix, Does.Contain("DOTNET_MULTILEVEL_LOOKUP=0"));
    }
}
