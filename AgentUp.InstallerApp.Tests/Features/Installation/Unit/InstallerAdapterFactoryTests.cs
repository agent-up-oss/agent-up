using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.MacOs;
using AgentUp.Installers.Features.Ubuntu;
using AgentUp.Installers.Features.Windows;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Unit;

[TestFixture]
public class InstallerAdapterFactoryTests
{
    private string? _realUbuntu;
    private string? _payloadRoot;
    private string? _realMacOs;
    private string? _realWindows;

    [SetUp]
    public void SetUp()
    {
        _realUbuntu = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_REAL_UBUNTU");
        _realMacOs = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_REAL_MACOS");
        _realWindows = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_REAL_WINDOWS");
        _payloadRoot = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_UBUNTU", _realUbuntu);
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_MACOS", _realMacOs);
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_WINDOWS", _realWindows);
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT", _payloadRoot);
    }

    [Test]
    public void Create_usesDryRunAdapterByDefault()
    {
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_UBUNTU", null);
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT", null);

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<FakeInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_usesUbuntuAdapterOnLinuxWhenExplicitlyEnabledAndPayloadIsProvided()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Ubuntu adapter selection is Linux-specific.");

        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_UBUNTU", "1");
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT", "/payload");

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<UbuntuInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_usesMacOsAdapterOnMacOsWhenExplicitlyEnabledAndPayloadIsProvided()
    {
        if (!OperatingSystem.IsMacOS())
            Assert.Ignore("macOS adapter selection is macOS-specific.");

        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_MACOS", "1");
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT", "/payload");

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<MacOsInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_usesWindowsAdapterOnWindowsWhenExplicitlyEnabledAndPayloadIsProvided()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Windows adapter selection is Windows-specific.");

        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_WINDOWS", "1");
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT", @"C:\payload");

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<WindowsInstallerPlatformAdapter>());
    }
}
