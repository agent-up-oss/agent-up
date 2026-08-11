using LocalInstaller.Core.Composition;
using LocalInstaller.Core.Features.Installation.Interfaces;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.Installation.Providers;
using LocalInstaller.Core.Features.NixOsInstallation.Providers;
using LocalInstaller.Core.Features.UbuntuInstallation.Providers;
using LocalInstaller.Core.Tests.Support;

namespace LocalInstaller.Core.Tests.Features.Installation.Provider;

[TestFixture]
public class InstallerPlatformAdapterFactoryTests
{
    private string? _fakeInstaller;
    private string? _payloadRoot;
    private string? _nixOsLookupOnly;

    private static ProductManifest Product => AgentUpTestManifests.Product();
    private static string FakeInstallerVariable => Product.FakeInstallerVariable;
    private static string PayloadRootVariable => Product.PayloadRootVariable;
    private const string NixOsLookupOnlyVariable = "AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY";

    [SetUp]
    public void SetUp()
    {
        _fakeInstaller = Environment.GetEnvironmentVariable(FakeInstallerVariable);
        _payloadRoot = Environment.GetEnvironmentVariable(PayloadRootVariable);
        _nixOsLookupOnly = Environment.GetEnvironmentVariable(NixOsLookupOnlyVariable);
        Environment.SetEnvironmentVariable(NixOsLookupOnlyVariable, null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(FakeInstallerVariable, _fakeInstaller);
        Environment.SetEnvironmentVariable(PayloadRootVariable, _payloadRoot);
        Environment.SetEnvironmentVariable(NixOsLookupOnlyVariable, _nixOsLookupOnly);
    }

    [Test]
    public void Create_returnsFakeAdapterWhenExplicitlyRequested()
    {
        Environment.SetEnvironmentVariable(FakeInstallerVariable, "1");
        Environment.SetEnvironmentVariable(PayloadRootVariable, null);

        var adapter = CreateAgentUpAdapter();

        Assert.That(adapter, Is.TypeOf<FakeInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_requiresPayloadRootForDefaultRealAdapter()
    {
        if (OperatingSystem.IsLinux() && InstallerPlatformAdapterFactory.IsNixOsHost())
            Assert.Ignore("NixOS lookup-only mode does not require installer payloads.");

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, null);

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
            Environment.SetEnvironmentVariable(PayloadRootVariable, null);
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
            Environment.SetEnvironmentVariable(PayloadRootVariable, null);
            Directory.CreateDirectory(Path.Join(root, "payload", "desktop"));
            Directory.CreateDirectory(Path.Join(root, "payload", "server"));
            Directory.CreateDirectory(Path.Join(root, "payload", "cli"));

            Assert.That(
                () => InstallerPlatformAdapterFactory.ResolvePayloadRoot(root, AgentUpTestManifests.Product()),
                Throws.InvalidOperationException.With.Message.Contains("registered installer option directories"));
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

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, "/payload");

        var adapter = CreateAgentUpAdapter();

        Assert.That(adapter, Is.TypeOf<UbuntuInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_returnsNixOsLookupOnlyAdapterWhenExplicitlyRequested()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("NixOS adapter selection is Linux-specific.");

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, null);
        Environment.SetEnvironmentVariable(NixOsLookupOnlyVariable, "1");

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
            Environment.GetEnvironmentVariable(FakeInstallerVariable),
            Environment.GetEnvironmentVariable(NixOsLookupOnlyVariable) == "1" || InstallerPlatformAdapterFactory.IsNixOsHost());
    }
}
