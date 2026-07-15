using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Ubuntu;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Unit;

[TestFixture]
public class InstallerAdapterFactoryTests
{
    private string? _realUbuntu;
    private string? _payloadRoot;

    [SetUp]
    public void SetUp()
    {
        _realUbuntu = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_REAL_UBUNTU");
        _payloadRoot = Environment.GetEnvironmentVariable("AGENTUP_INSTALLER_PAYLOAD_ROOT");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("AGENTUP_INSTALLER_REAL_UBUNTU", _realUbuntu);
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
}
