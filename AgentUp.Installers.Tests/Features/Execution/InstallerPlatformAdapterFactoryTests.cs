using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Ubuntu;

namespace AgentUp.Installers.Tests.Features.Execution;

[TestFixture]
public class InstallerPlatformAdapterFactoryTests
{
    private string? _fakeInstaller;
    private string? _payloadRoot;

    [SetUp]
    public void SetUp()
    {
        _fakeInstaller = Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable);
        _payloadRoot = Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, _fakeInstaller);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, _payloadRoot);
    }

    [Test]
    public void Create_returnsFakeAdapterWhenExplicitlyRequested()
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, "1");
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);

        var adapter = InstallerPlatformAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<FakeInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_requiresPayloadRootForDefaultRealAdapter()
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);

        Assert.That(
            () => InstallerPlatformAdapterFactory.Create(),
            Throws.InvalidOperationException.With.Message.Contains(InstallerPlatformAdapterFactory.PayloadRootVariable));
    }

    [Test]
    public void Create_returnsLinuxAdapterByDefaultWhenPayloadRootIsProvided()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Linux adapter selection is Linux-specific.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, "/payload");

        var adapter = InstallerPlatformAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<UbuntuInstallerPlatformAdapter>());
    }
}
