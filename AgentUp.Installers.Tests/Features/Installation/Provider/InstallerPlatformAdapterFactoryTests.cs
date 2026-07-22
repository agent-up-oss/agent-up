using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.NixOsInstallation.Providers;
using AgentUp.Installers.Features.UbuntuInstallation;
using AgentUp.Installers.Features.UbuntuInstallation.Providers;

namespace AgentUp.Installers.Tests.Features.Installation.Provider;

[TestFixture]
public class InstallerPlatformAdapterFactoryTests
{
    private string? _fakeInstaller;
    private string? _payloadRoot;
    private string? _nixOsLookupOnly;

    [SetUp]
    public void SetUp()
    {
        _fakeInstaller = Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable);
        _payloadRoot = Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable);
        _nixOsLookupOnly = Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.NixOsLookupOnlyVariable);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.NixOsLookupOnlyVariable, null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, _fakeInstaller);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, _payloadRoot);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.NixOsLookupOnlyVariable, _nixOsLookupOnly);
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
        if (OperatingSystem.IsLinux() && InstallerPlatformAdapterFactory.UseNixOsLookupOnlyMode())
            Assert.Ignore("NixOS lookup-only mode does not require installer payloads.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);

        Assert.That(
            () => InstallerPlatformAdapterFactory.Create(),
            Throws.InvalidOperationException.With.Message.Contains(InstallerPlatformAdapterFactory.PayloadRootVariable));
    }

    [Test]
    public void ResolvePayloadRoot_usesBundledPayloadNextToInstallerExecutableWhenEnvironmentIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerPlatformAdapterFactoryTests", Guid.NewGuid().ToString());

        try
        {
            Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);
            Directory.CreateDirectory(Path.Join(root, "payload", "desktop"));
            Directory.CreateDirectory(Path.Join(root, "payload", "server"));
            Directory.CreateDirectory(Path.Join(root, "payload", "cli"));

            var payloadRoot = InstallerPlatformAdapterFactory.ResolvePayloadRoot(root);

            Assert.That(payloadRoot, Is.EqualTo(Path.Join(root, "payload")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void PayloadCandidateDirectories_includesAppBaseDirectoryAndProcessExecutableDirectory()
    {
        var appBaseDirectory = Path.Join(Path.GetTempPath(), "AgentUp-InstallerPlatformAdapterFactoryTests", Guid.NewGuid().ToString());

        var candidates = InstallerPlatformAdapterFactory.PayloadCandidateDirectories(appBaseDirectory);

        Assert.That(candidates, Does.Contain(Path.GetFullPath(appBaseDirectory)));
        Assert.That(candidates, Does.Contain(Path.GetDirectoryName(Environment.ProcessPath!)));
    }

    [Test]
    public void Create_returnsLinuxAdapterByDefaultWhenPayloadRootIsProvided()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Linux adapter selection is Linux-specific.");
        if (InstallerPlatformAdapterFactory.UseNixOsLookupOnlyMode())
            Assert.Ignore("NixOS uses the lookup-only adapter instead of the Ubuntu installer adapter.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, "/payload");

        var adapter = InstallerPlatformAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<UbuntuInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_returnsNixOsLookupOnlyAdapterWhenExplicitlyRequested()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("NixOS adapter selection is Linux-specific.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.NixOsLookupOnlyVariable, "1");

        var adapter = InstallerPlatformAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<NixOsInstallerPlatformAdapter>());
        Assert.That(adapter.SupportsInstallActions, Is.False);
    }
}
