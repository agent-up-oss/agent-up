using AgentUp.Tests.Fixtures.Linux;

namespace AgentUp.Tests.Features.Browser.E2E;

[TestFixture, Category("E2E")]
[Platform(Include = "Linux")]
public sealed class LinuxDesktopFixtureIsolationTests
{
    [Test]
    public void Native_display_suite_does_not_attach_to_the_session_desktop()
    {
        if (Environment.GetEnvironmentVariable("AGENTUP_E2E_USE_SESSION_DISPLAY") == "1")
            Assert.Ignore("Session display opt-in is active.");

        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        Assert.That(runtimeDir, Is.Not.Null.And.Not.Empty,
            "GTK would otherwise find the session Wayland socket at $XDG_RUNTIME_DIR/wayland-0.");
        Assert.That(runtimeDir, Does.Contain("agentup-e2e"),
            "GTK would otherwise find the session Wayland socket at $XDG_RUNTIME_DIR/wayland-0.");

        var wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        var dbus = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Join(runtimeDir, "wayland-0")), Is.False);
            Assert.That(wayland, Is.Not.Null.And.Not.Empty,
                "Leaving WAYLAND_DISPLAY empty lets GTK default to wayland-0.");
            Assert.That(wayland, Does.Not.StartWith("wayland-"),
                "A compositor socket name would attach to a real Wayland display.");
            Assert.That(
                wayland == LinuxDesktopFixtureAdapter.IsolatedWaylandDisplayName
                || wayland!.StartsWith("/proc/", StringComparison.Ordinal),
                $"WAYLAND_DISPLAY={wayland} is neither the fixture sentinel nor the Nix GTK fake display.");
            Assert.That(Environment.GetEnvironmentVariable("GDK_BACKEND"), Is.EqualTo("x11"));
            Assert.That(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), Is.EqualTo("x11"));
            Assert.That(Environment.GetEnvironmentVariable("GTK_USE_PORTAL"), Is.EqualTo("0"));
            Assert.That(Environment.GetEnvironmentVariable("DISPLAY"), Is.EqualTo(LinuxDesktopFixtureAdapter.IsolatedDisplay));
            Assert.That(Environment.GetEnvironmentVariable("DISPLAY"), Does.Match(@"^:[1-9]\d{2,}$"),
                "Linux E2E must run on a fixture-owned Xvfb display numbered 100 or higher, never session :0.");
            Assert.That(dbus, Does.Contain(runtimeDir),
                "libdbus falls back to the session bus when DBUS_SESSION_BUS_ADDRESS is unset.");
        });
    }
}
