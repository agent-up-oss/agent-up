using AgentUp.InstallerConfig;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Models;
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
        _fakeInstaller = Environment.GetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable);
        _payloadRoot = Environment.GetEnvironmentVariable(AgentUpProduct.PayloadRootVariable);
        _nixOsLookupOnly = Environment.GetEnvironmentVariable(AgentUpProduct.NixOsLookupOnlyVariable);
        Environment.SetEnvironmentVariable(AgentUpProduct.NixOsLookupOnlyVariable, null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable, _fakeInstaller);
        Environment.SetEnvironmentVariable(AgentUpProduct.PayloadRootVariable, _payloadRoot);
        Environment.SetEnvironmentVariable(AgentUpProduct.NixOsLookupOnlyVariable, _nixOsLookupOnly);
    }

    [Test]
    public void Create_returnsFakeAdapterWhenExplicitlyRequested()
    {
        Environment.SetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable, "1");
        Environment.SetEnvironmentVariable(AgentUpProduct.PayloadRootVariable, null);

        var adapter = CreateAgentUpAdapter();

        Assert.That(adapter, Is.TypeOf<FakeInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_requiresPayloadRootForDefaultRealAdapter()
    {
        if (OperatingSystem.IsLinux() && InstallerPlatformAdapterFactory.IsNixOsHost())
            Assert.Ignore("NixOS lookup-only mode does not require installer payloads.");

        Environment.SetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(AgentUpProduct.PayloadRootVariable, null);

        var product = AgentUpTestManifests.Product();
        Assert.That(
            () => InstallerPlatformAdapterFactory.Create(product, AppContext.BaseDirectory, null, false),
            Throws.InvalidOperationException.With.Message.Contains(product.PayloadRootVariable));
    }

    [Test]
    public void ResolvePayloadRoot_usesBundledPayloadNextToInstallerExecutableWhenEnvironmentIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerPlatformAdapterFactoryTests", Guid.NewGuid().ToString());

        try
        {
            Environment.SetEnvironmentVariable(AgentUpProduct.PayloadRootVariable, null);
            Directory.CreateDirectory(Path.Join(root, "payload", "desktop"));
            Directory.CreateDirectory(Path.Join(root, "payload", "server"));
            Directory.CreateDirectory(Path.Join(root, "payload", "cli"));
            Directory.CreateDirectory(Path.Join(root, "payload", "tray"));

            var payloadRoot = InstallerPlatformAdapterFactory.ResolvePayloadRoot(root, AgentUpTestManifests.Product());

            Assert.That(payloadRoot, Is.EqualTo(Path.Join(root, "payload")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ResolvePayloadRoot_rejectsBundledPayloadWithoutTrayDirectory()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerPlatformAdapterFactoryTests", Guid.NewGuid().ToString());

        try
        {
            Environment.SetEnvironmentVariable(AgentUpProduct.PayloadRootVariable, null);
            Directory.CreateDirectory(Path.Join(root, "payload", "desktop"));
            Directory.CreateDirectory(Path.Join(root, "payload", "server"));
            Directory.CreateDirectory(Path.Join(root, "payload", "cli"));

            Assert.That(
                () => InstallerPlatformAdapterFactory.ResolvePayloadRoot(root, AgentUpTestManifests.Product()),
                Throws.InvalidOperationException.With.Message.Contains("desktop, server, cli, and tray directories"));
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
        if (InstallerPlatformAdapterFactory.IsNixOsHost())
            Assert.Ignore("NixOS uses the lookup-only adapter instead of the Ubuntu installer adapter.");

        Environment.SetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(AgentUpProduct.PayloadRootVariable, "/payload");

        var adapter = CreateAgentUpAdapter();

        Assert.That(adapter, Is.TypeOf<UbuntuInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_returnsNixOsLookupOnlyAdapterWhenExplicitlyRequested()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("NixOS adapter selection is Linux-specific.");

        Environment.SetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(AgentUpProduct.PayloadRootVariable, null);
        Environment.SetEnvironmentVariable(AgentUpProduct.NixOsLookupOnlyVariable, "1");

        var adapter = CreateAgentUpAdapter();

        Assert.That(adapter, Is.TypeOf<NixOsInstallerPlatformAdapter>());
        Assert.That(adapter.SupportsInstallActions, Is.False);
    }

    private static IInstallerPlatformAdapter CreateAgentUpAdapter()
    {
        var product = AgentUpTestManifests.Product();
        return InstallerPlatformAdapterFactory.Create(
            product,
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable),
            Environment.GetEnvironmentVariable(AgentUpProduct.NixOsLookupOnlyVariable) == "1" || InstallerPlatformAdapterFactory.IsNixOsHost());
    }
}
