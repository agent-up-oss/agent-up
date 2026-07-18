using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Factories;
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

namespace AgentUp.InstallerApp.Tests.Features.Installation.TerminalIntegration;

[TestFixture]
public class InstallerAdapterFactoryTests
{
    private string? _payloadRoot;
    private string? _fakeInstaller;
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
    public void Create_usesFakeAdapterWhenExplicitlyEnabled()
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, "1");
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<FakeInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_requiresPayloadRootForRealInstaller()
    {
        if (OperatingSystem.IsLinux() && InstallerPlatformAdapterFactory.UseNixOsLookupOnlyMode())
            Assert.Ignore("NixOS lookup-only mode does not require installer payloads.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);

        Assert.That(
            () => InstallerAdapterFactory.Create(),
            Throws.InvalidOperationException.With.Message.Contains(InstallerPlatformAdapterFactory.PayloadRootVariable));
    }

    [Test]
    public void ResolvePayloadRoot_usesBundledPayloadNextToInstallerExecutableWhenEnvironmentIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerAdapterFactoryTests", Guid.NewGuid().ToString());

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
    public void ResolvePayloadRoot_prefersExplicitEnvironmentPayload()
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, "/payload");

        var payloadRoot = InstallerPlatformAdapterFactory.ResolvePayloadRoot("/app");

        Assert.That(payloadRoot, Is.EqualTo("/payload"));
    }

    [Test]
    public void Program_skipsBundledPayloadResolutionWhenNixOsLookupOnlyModeIsEnabled()
    {
        var program = File.ReadAllText(Path.Join(FindRepositoryRoot(TestContext.CurrentContext.TestDirectory), "AgentUp.InstallerApp", "Program.cs"));

        Assert.That(program, Does.Contain("InstallerPlatformAdapterFactory.UseNixOsLookupOnlyMode()"));
        Assert.That(program, Does.Contain("return;"));
    }

    [Test]
    public void Create_usesUbuntuAdapterByDefaultOnLinuxWhenPayloadIsProvided()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Ubuntu adapter selection is Linux-specific.");
        if (InstallerPlatformAdapterFactory.UseNixOsLookupOnlyMode())
            Assert.Ignore("NixOS uses the lookup-only adapter instead of the Ubuntu installer adapter.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, "/payload");

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<UbuntuInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_usesNixOsAdapterWhenLookupOnlyModeIsExplicitlyEnabled()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("NixOS adapter selection is Linux-specific.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.NixOsLookupOnlyVariable, "1");

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<NixOsInstallerPlatformAdapter>());
        Assert.That(adapter.SupportsInstallActions, Is.False);
    }

    [Test]
    public void Create_usesMacOsAdapterByDefaultOnMacOsWhenPayloadIsProvided()
    {
        if (!OperatingSystem.IsMacOS())
            Assert.Ignore("macOS adapter selection is macOS-specific.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, "/payload");

        var adapter = InstallerAdapterFactory.Create();

        Assert.That(adapter, Is.TypeOf<MacOsInstallerPlatformAdapter>());
    }

    [Test]
    public void Create_usesWindowsAdapterByDefaultOnWindowsWhenPayloadIsProvided()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Windows adapter selection is Windows-specific.");

        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.FakeInstallerVariable, null);
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, @"C:\payload");

        var adapter = InstallerAdapterFactory.Create();

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
