using AgentUp.Tray.Features.AutoStart;

namespace AgentUp.Tray.Tests.Features.AutoStart.Unit;

[TestFixture]
public sealed class AutoStartRegistrarFactoryTests
{
    private const string BaseDirectory = "/opt/agent-up/tray";

    [Test]
    public void Create_returnsTheLinuxRegistrarForLinux()
    {
        Assert.That(AutoStartRegistrarFactory.Create("linux", BaseDirectory),
            Is.InstanceOf<LinuxAutoStartRegistrar>());
    }

    [Test]
    public void Create_returnsTheMacOsRegistrarForMacOs()
    {
        Assert.That(AutoStartRegistrarFactory.Create("macos", BaseDirectory),
            Is.InstanceOf<MacOsAutoStartRegistrar>());
    }

    [Test]
    public void Create_returnsNothingForAnUnsupportedPlatform()
    {
        Assert.That(AutoStartRegistrarFactory.Create("freebsd", BaseDirectory), Is.Null);
    }

    [TestCase("windows", "AgentUp.Tray.exe")]
    [TestCase("linux", "AgentUp.Tray")]
    [TestCase("macos", "AgentUp.Tray")]
    public void TrayExecutableName_appendsTheExtensionOnlyOnWindows(string platformId, string expected)
    {
        Assert.That(AutoStartRegistrarFactory.TrayExecutableName(platformId), Is.EqualTo(expected));
    }

    [Test]
    public void CurrentPlatformId_reportsOneOfTheSupportedPlatforms()
    {
        Assert.That(AutoStartRegistrarFactory.CurrentPlatformId(),
            Is.AnyOf("windows", "macos", "linux", "unsupported"));
    }

    [Test]
    public void Create_withoutArgumentsAgreesWithTheExplicitOverloadForThisHost()
    {
        // The parameterless overload is the production wiring: it must select the same
        // registrar the explicit overload does for the platform it detects.
        var detected = AutoStartRegistrarFactory.CurrentPlatformId();

        Assert.That(AutoStartRegistrarFactory.Create()?.GetType(),
            Is.EqualTo(AutoStartRegistrarFactory.Create(detected, AppContext.BaseDirectory)?.GetType()));
    }
}
