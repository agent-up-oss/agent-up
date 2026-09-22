using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Common.Tests.Features.NixRuntime.Unit;

[TestFixture]
public sealed class FirstPartyNixPinTests
{
    [Test]
    public void DefaultNix_puts_package_bin_and_dev_root_on_PATH()
    {
        var nix = FirstPartyNixPin.DefaultNix(["nodejs_22"]);

        Assert.That(nix, Does.Contain(FirstPartyNixPin.Rev));
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

    /// <summary>
    /// A package is enabled into whatever Server holds it, and a freshly installed Nix has no
    /// nixpkgs channel, so the shell has to name the commit it wants rather than ask NIX_PATH.
    /// </summary>
    [Test]
    public void DefaultNix_pins_nixpkgs_to_the_recorded_commit_instead_of_the_host_channel()
    {
        var nix = FirstPartyNixPin.DefaultNix(["nodejs_22"]);

        Assert.Multiple(() =>
        {
            Assert.That(nix, Does.Not.Contain("<nixpkgs>"));
            Assert.That(nix, Does.Contain("builtins.fetchTarball"));
            Assert.That(
                nix,
                Does.Contain($"https://github.com/NixOS/nixpkgs/archive/{FirstPartyNixPin.Rev}.tar.gz"));
        });
    }

    [Test]
    public void DefaultNix_falls_back_to_the_host_channel_when_no_commit_is_recorded()
    {
        var nix = FirstPartyNixPin.DefaultNix(["nodejs_22"], nixpkgsRev: null);

        Assert.That(nix, Does.Contain("import <nixpkgs> {}"));
        Assert.That(nix, Does.Not.Contain("fetchTarball"));
    }

    // The pin is fetched now rather than decorative, so a value that is not a commit is a broken
    // package rather than a cosmetic problem. This catches a truncated or mistyped rev; a rev that
    // is well-formed but absent from nixpkgs can only be caught by fetching it, which is what the
    // runtime capability E2E does - and is how the mangled nixos-24.05 head that shipped here was
    // found, by GitHub answering 404.
    [Test]
    public void The_recorded_commit_is_a_full_git_object_name()
    {
        Assert.That(FirstPartyNixPin.Rev, Has.Length.EqualTo(40));
        Assert.That(FirstPartyNixPin.Rev, Does.Match("^[0-9a-f]{40}$"));
    }

    [Test]
    public void The_recorded_commit_is_the_one_the_packed_manifest_carries()
    {
        Assert.That(FirstPartyNixPin.Nixpkgs.Rev, Is.EqualTo(FirstPartyNixPin.Rev));
    }

    // The runtime opens these two by soname rather than through a linked RPATH, so nixpkgs cannot
    // patch them in and the shell has to. The Example API and Sample Desktop died on
    // "https://aka.ms/dotnet-missing-libicu", and once that was delivered the API died on
    // "No usable version of libssl was found" connecting to Postgres. The SDK is not enough on
    // its own.
    [Test]
    public void DefaultNix_delivers_the_dlopened_native_libraries_alongside_the_dotnet_sdk()
    {
        var nix = FirstPartyNixPin.DefaultNix(["dotnet-sdk_10"]);

        Assert.That(nix, Does.Contain("packages = [ pkgs.dotnet-sdk_10 pkgs.icu pkgs.openssl.out ];"));
        Assert.That(
            nix,
            Does.Contain("export LD_LIBRARY_PATH=\"${pkgs.icu}/lib:${pkgs.openssl.out}/lib"));
    }

    // openssl lists its bin output first, so a bare ${pkgs.openssl} would name a store path with
    // no lib directory in it and resolve nothing.
    [Test]
    public void DefaultNix_names_the_openssl_output_that_holds_the_libraries()
    {
        var nix = FirstPartyNixPin.DefaultNix(["dotnet-sdk_10"]);

        Assert.That(nix, Does.Not.Contain("${pkgs.openssl}"));
    }

    // Only a dotnet module needs them; a docker or node module must not grow an unrelated input.
    [Test]
    public void DefaultNix_leaves_a_module_without_the_sdk_alone()
    {
        var nix = FirstPartyNixPin.DefaultNix(["nodejs_22"]);

        Assert.Multiple(() =>
        {
            Assert.That(nix, Does.Not.Contain("icu"));
            Assert.That(nix, Does.Not.Contain("openssl"));
        });
    }

    // A capability shell runs tools. Pulling stdenv in meant every host that enabled a module
    // downloaded gcc, binutils, perl and the autotools hooks before the first application started.
    [Test]
    public void DefaultNix_builds_a_shell_without_the_c_toolchain()
    {
        var nix = FirstPartyNixPin.DefaultNix(["dotnet-sdk_10"]);

        Assert.That(nix, Does.Contain("pkgs.mkShellNoCC {"));
        Assert.That(nix, Does.Not.Contain("pkgs.mkShell {"));
    }

    // The expansion has to reach the shell, not the Nix evaluator, so it carries the '' escape.
    [Test]
    public void DefaultNix_escapes_the_library_path_expansion_for_nix()
    {
        var nix = FirstPartyNixPin.DefaultNix(["dotnet-sdk_10"]);

        Assert.That(nix, Does.Contain("''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"));
    }
}
