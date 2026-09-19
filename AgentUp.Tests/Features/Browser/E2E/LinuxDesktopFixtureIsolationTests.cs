using AgentUp.Tests.Fixtures.Linux;

namespace AgentUp.Tests.Features.Browser.E2E;

[TestFixture, Category("E2E")]
[Platform(Include = "Linux")]
public sealed class LinuxDesktopFixtureIsolationTests
{
    [Test]
    public void Native_display_suite_runs_on_its_own_runtime_directory()
    {
        AssertIsolationIsExpected();

        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

        Assert.Multiple(() =>
        {
            Assert.That(runtimeDir, Is.Not.Null.And.Not.Empty, RuntimeDirReason);
            Assert.That(runtimeDir, Does.Contain("agentup-e2e"), RuntimeDirReason);
            Assert.That(File.Exists(Path.Join(runtimeDir, "wayland-0")), Is.False, RuntimeDirReason);
        });
    }

    [Test]
    public void Native_display_suite_does_not_attach_to_a_session_wayland_compositor()
    {
        AssertIsolationIsExpected();

        var wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

        Assert.Multiple(() =>
        {
            Assert.That(wayland, Is.Not.Null.And.Not.Empty,
                "Leaving WAYLAND_DISPLAY empty lets GTK default to wayland-0.");
            Assert.That(wayland, Does.Not.StartWith("wayland-"),
                "A compositor socket name would attach to a real Wayland display.");
            Assert.That(
                wayland == LinuxDesktopFixtureAdapter.IsolatedWaylandDisplayName
                || wayland!.StartsWith("/proc/", StringComparison.Ordinal),
                $"WAYLAND_DISPLAY={wayland} is neither the fixture sentinel nor the Nix GTK fake display.");
        });
    }

    [Test]
    public void Native_display_suite_forces_the_toolkits_onto_x11()
    {
        AssertIsolationIsExpected();

        Assert.Multiple(() =>
        {
            Assert.That(Environment.GetEnvironmentVariable("GDK_BACKEND"), Is.EqualTo("x11"));
            Assert.That(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), Is.EqualTo("x11"));
            Assert.That(Environment.GetEnvironmentVariable("GTK_USE_PORTAL"), Is.EqualTo("0"));
        });
    }

    [Test]
    public void Native_display_suite_runs_on_a_fixture_owned_xvfb_display()
    {
        AssertIsolationIsExpected();

        var display = Environment.GetEnvironmentVariable("DISPLAY");

        Assert.Multiple(() =>
        {
            Assert.That(display, Is.EqualTo(LinuxDesktopFixtureAdapter.IsolatedDisplay));
            Assert.That(display, Does.Match(@"^:[1-9]\d{2,}$"),
                "Linux E2E must run on a fixture-owned Xvfb display numbered 100 or higher, never session :0.");
        });
    }

    [Test]
    public void Native_display_suite_runs_on_its_own_session_bus()
    {
        AssertIsolationIsExpected();

        Assert.That(
            Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"),
            Does.Contain(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")!),
            "libdbus falls back to the session bus when DBUS_SESSION_BUS_ADDRESS is unset.");
    }

    private const string RuntimeDirReason =
        "GTK would otherwise find the session Wayland socket at $XDG_RUNTIME_DIR/wayland-0.";

    private static void AssertIsolationIsExpected()
    {
        if (Environment.GetEnvironmentVariable("AGENTUP_E2E_USE_SESSION_DISPLAY") == "1")
            Assert.Ignore("Session display opt-in is active.");
    }
}
