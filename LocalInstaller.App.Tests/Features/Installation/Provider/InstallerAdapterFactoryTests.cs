using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Composition;
using AgentUp.InstallerApp.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.MacOsInstallation;
using AgentUp.Installers.Features.MacOsInstallation.Providers;
using AgentUp.Installers.Features.NixOsInstallation.Providers;
using AgentUp.Installers.Features.UbuntuInstallation;
using AgentUp.Installers.Features.UbuntuInstallation.Providers;
using AgentUp.Installers.Features.WindowsInstallation;
using AgentUp.Installers.Features.WindowsInstallation.Providers;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Provider;

[TestFixture]
public class InstallerAdapterFactoryTests
{
    private string? _payloadRoot;
    private string? _fakeInstaller;
    private string? _nixOsLookupOnly;

    private static ProductManifest Product => AgentUpInstallerAppTestManifests.Product();
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
    public void Create_usesFakeAdapterWhenExplicitlyEnabled()
    {
        Environment.SetEnvironmentVariable(FakeInstallerVariable, "1");
        Environment.SetEnvironmentVariable(PayloadRootVariable, null);

        var adapter = InstallerAdapterFactory.Create(Product, FakeInstallerVariable, NixOsLookupOnlyVariable);

        Assert.That(adapter, Is.TypeOf<FakeInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_requiresPayloadRootForRealInstaller()
    {
        if (OperatingSystem.IsLinux() && InstallerAdapterFactory.UseNixOsLookupOnlyMode())
            Assert.Ignore("NixOS lookup-only mode does not require installer payloads.");

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, null);

        Assert.That(
            () => InstallerAdapterFactory.Create(Product, FakeInstallerVariable, NixOsLookupOnlyVariable),
            Throws.InvalidOperationException.With.Message.Contains(PayloadRootVariable));
    }

    [Test]
    public void ResolvePayloadRoot_usesBundledPayloadNextToInstallerExecutableWhenEnvironmentIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerAdapterFactoryTests", Guid.NewGuid().ToString());

        try
        {
            Environment.SetEnvironmentVariable(PayloadRootVariable, null);
            Directory.CreateDirectory(Path.Join(root, "payload", "desktop"));
            Directory.CreateDirectory(Path.Join(root, "payload", "server"));
            Directory.CreateDirectory(Path.Join(root, "payload", "cli"));
            Directory.CreateDirectory(Path.Join(root, "payload", "tray"));

            var payloadRoot = InstallerPlatformAdapterFactory.ResolvePayloadRoot(root, AgentUpInstallerAppTestManifests.Product());

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
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerAdapterFactoryTests", Guid.NewGuid().ToString());

        try
        {
            Environment.SetEnvironmentVariable(PayloadRootVariable, null);
            Directory.CreateDirectory(Path.Join(root, "payload", "desktop"));
            Directory.CreateDirectory(Path.Join(root, "payload", "server"));
            Directory.CreateDirectory(Path.Join(root, "payload", "cli"));

            Assert.That(
                () => InstallerPlatformAdapterFactory.ResolvePayloadRoot(root, AgentUpInstallerAppTestManifests.Product()),
                Throws.InvalidOperationException.With.Message.Contains("desktop, server, cli, and tray directories"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ResolvePayloadRoot_prefersExplicitEnvironmentPayload()
    {
        Environment.SetEnvironmentVariable(PayloadRootVariable, "/payload");

        var payloadRoot = InstallerPlatformAdapterFactory.ResolvePayloadRoot("/app", AgentUpInstallerAppTestManifests.Product());

        Assert.That(payloadRoot, Is.EqualTo("/payload"));
    }

    [Test]
    public void SampleInstallerProgram_usesFluentApiWithoutPlatformAdapterLogic()
    {
        var program = File.ReadAllText(Path.Join(FindRepositoryRoot(TestContext.CurrentContext.TestDirectory), "LocalInstaller.Sample.InstallerApp", "Program.cs"));

        Assert.That(program, Does.Contain("LocalInstallerApp.Create(args)"));
        Assert.That(program, Does.Contain(".RunAsync<App>()"));
        Assert.That(program, Does.Not.Contain("InstallerPlatformAdapterFactory"));
        Assert.That(program, Does.Not.Contain("Windows"));
    }

    [Test]
    public void Create_usesUbuntuAdapterByDefaultOnLinuxWhenPayloadIsProvided()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Ubuntu adapter selection is Linux-specific.");
        if (InstallerAdapterFactory.UseNixOsLookupOnlyMode())
            Assert.Ignore("NixOS uses the lookup-only adapter instead of the Ubuntu installer adapter.");

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, "/payload");

        var adapter = InstallerAdapterFactory.Create(Product, FakeInstallerVariable, NixOsLookupOnlyVariable);

        Assert.That(adapter, Is.TypeOf<UbuntuInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_usesNixOsAdapterWhenLookupOnlyModeIsExplicitlyEnabled()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("NixOS adapter selection is Linux-specific.");

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, null);
        Environment.SetEnvironmentVariable(NixOsLookupOnlyVariable, "1");

        var adapter = InstallerAdapterFactory.Create(Product, FakeInstallerVariable, NixOsLookupOnlyVariable);

        Assert.That(adapter, Is.TypeOf<NixOsInstallerPlatformAdapter>());
        Assert.That(adapter.SupportsInstallActions, Is.False);
    }

    [Test]
    public void Create_usesMacOsAdapterByDefaultOnMacOsWhenPayloadIsProvided()
    {
        if (!OperatingSystem.IsMacOS())
            Assert.Ignore("macOS adapter selection is macOS-specific.");

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, "/payload");

        var adapter = InstallerAdapterFactory.Create(Product, FakeInstallerVariable, NixOsLookupOnlyVariable);

        Assert.That(adapter, Is.TypeOf<MacOsInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_usesWindowsAdapterByDefaultOnWindowsWhenPayloadIsProvided()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Windows adapter selection is Windows-specific.");

        Environment.SetEnvironmentVariable(FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(PayloadRootVariable, @"C:\payload");

        var adapter = InstallerAdapterFactory.Create(Product, FakeInstallerVariable, NixOsLookupOnlyVariable);

        Assert.That(adapter, Is.TypeOf<WindowsInstallerPlatformAdapter>());
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find repository root from {startDirectory}.");
    }
}
