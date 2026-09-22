using AgentUp.Tests.Fixtures.Linux;

namespace AgentUp.Tests.Features.DesktopApplications.Provider;

/// <summary>
/// The native desktop fixture decides whether to load this host's GUI libraries out of the
/// <c>shell.nix</c> closure. Getting that wrong does not fail an assertion, it segfaults the test
/// host, so the decision is checked here rather than observed through a run.
/// </summary>
[TestFixture]
public sealed class LinuxDesktopFixtureNixImportTests
{
    [Test]
    public void A_nixos_host_imports_its_gui_libraries_from_shell_nix()
    {
        var import = LinuxDesktopFixtureAdapter.ShouldImportNixEnvironment(
            path => path == "/etc/NIXOS",
            _ => null);

        Assert.That(import, Is.True);
    }

    // A runner with Nix installed for capability launches still hosts Ubuntu's GTK and WebKit.
    // Loading the Nix closure beside them is what crashed the runtime capability job.
    [Test]
    public void A_host_with_nix_installed_but_system_gui_libraries_does_not()
    {
        var import = LinuxDesktopFixtureAdapter.ShouldImportNixEnvironment(
            _ => false,
            name => name == "NIX_PATH" ? "/nix/var/nix/profiles/per-user/root/channels" : null);

        Assert.That(import, Is.False);
    }

    [TestCase("1", true)]
    [TestCase("true", true)]
    [TestCase("True", true)]
    [TestCase("0", false)]
    [TestCase("false", false)]
    public void The_override_decides_on_any_host(string flag, bool expected)
    {
        var import = LinuxDesktopFixtureAdapter.ShouldImportNixEnvironment(
            path => path == "/etc/NIXOS",
            name => name == LinuxDesktopFixtureAdapter.ImportNixFlag ? flag : null);

        Assert.That(import, Is.EqualTo(expected));
    }

    [Test]
    public void A_blank_override_leaves_the_host_to_decide()
    {
        var import = LinuxDesktopFixtureAdapter.ShouldImportNixEnvironment(
            path => path == "/etc/NIXOS",
            name => name == LinuxDesktopFixtureAdapter.ImportNixFlag ? "   " : null);

        Assert.That(import, Is.True);
    }
}
