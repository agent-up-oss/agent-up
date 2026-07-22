using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages.Models;
using System.Xml.Linq;
using WindowsInstallerManifest = AgentUp.Installers.Features.WindowsInstallation.Models.WindowsInstallerManifest;

namespace AgentUp.Packaging.Tests.Features.WindowsPackages.TerminalIntegration;

[TestFixture]
public class WindowsWixSourceGeneratorTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), "AgentUp-WixTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void ProductWxs_containsWindowsServicePathShortcutAndFiles()
    {
        var request = new PackageRequest(_root, "windows", "win-x64", "0.0.0-ci.149", "artifacts", "Release");
        var layout = WindowsPackageLayout.From(request);
        WritePublishedFile(layout.InstallerPublishDirectory, "AgentUp.InstallerApp.exe");
        WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Desktop.exe");
        WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
        WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");
        Directory.CreateDirectory(layout.InstallerSourceDirectory);
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, "agent-up.cmd"), "");

        var xml = new WindowsWixSourceGenerator(WindowsPackageManifest.From(request)).ProductWxs(layout);

        Assert.That(xml, Does.Contain("Name=\"Agent-Up\""));
        Assert.That(xml, Does.Contain("Version=\"0.0.1\""));
        Assert.That(xml, Does.Contain("EmbedCab=\"yes\""));
        Assert.That(xml, Does.Contain("Id=\"ARPNOMODIFY\""));
        Assert.That(xml, Does.Contain("Id=\"ARPNOREPAIR\""));
        Assert.That(xml, Does.Contain("ServiceInstall"));
        Assert.That(xml, Does.Contain("Name=\"agent-up-server\""));
        Assert.That(xml, Does.Not.Contain("Start=\"install\""));
        Assert.That(xml, Does.Not.Contain("Stop=\"both\""));
        Assert.That(xml, Does.Contain("Stop=\"uninstall\""));
        Assert.That(xml, Does.Contain("Arguments=\"--urls http://127.0.0.1:5000\""));
        Assert.That(xml, Does.Contain("Name=\"PATH\""));
        Assert.That(xml, Does.Contain("Shortcut"));
        Assert.That(xml, Does.Contain("Agent-Up Installer"));
        Assert.That(xml, Does.Contain("AgentUp.Desktop.exe"));
        Assert.That(xml, Does.Contain("AgentUp.Server.exe"));
        Assert.That(xml, Does.Contain("AgentUp.CLI.exe"));
        Assert.That(xml, Does.Contain("AgentUp.InstallerApp.exe"));
        Assert.That(xml, Does.Contain("InstallerPayloadDesktop"));
        Assert.That(xml, Does.Contain("InstallerPayloadServer"));
        Assert.That(xml, Does.Contain("InstallerPayloadCli"));
    }

    [Test]
    public void BundleWxs_chainsProductMsiWithoutLaunchingInstallerApp()
    {
        var request = new PackageRequest(_root, "windows", "win-x64", "1.2.3", "artifacts", "Release");
        var layout = WindowsPackageLayout.From(request);
        WritePublishedFile(layout.InstallerPublishDirectory, "AgentUp.InstallerApp.exe");
        WritePublishedFile(layout.InstallerPublishDirectory, "support.dll");
        WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Desktop.exe");
        WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
        WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");

        var xml = new WindowsWixSourceGenerator(WindowsPackageManifest.From(request)).BundleWxs(layout);

        Assert.That(xml, Does.Contain("WixStandardBootstrapperApplication"));
        Assert.That(xml, Does.Contain("Theme=\"rtfLicense\""));
        Assert.That(xml, Does.Contain(@"LaunchTarget=""[ProgramFiles64Folder]Agent-Up\installer\AgentUp.InstallerApp.exe"""));
        Assert.That(xml, Does.Contain(@"LaunchWorkingFolder=""[ProgramFiles64Folder]Agent-Up\installer"""));
        Assert.That(xml, Does.Contain("MsiPackage"));
        Assert.That(xml, Does.Contain("Product.msi"));
        Assert.That(xml, Does.Not.Contain("ExePackage"));
        Assert.That(xml, Does.Not.Contain("InstallArguments"));
        Assert.That(xml, Does.Not.Contain("UninstallArguments"));
        Assert.That(xml, Does.Not.Contain("--install-core"));
        Assert.That(xml, Does.Not.Contain("RegistrySearch"));
        Assert.That(xml, Does.Not.Contain("Win64="));
        Assert.That(xml, Does.Not.Contain("Permanent=\"yes\""));
        Assert.That(xml, Does.Contain("http://wixtoolset.org/schemas/v4/wxs/bal"));
    }

    [Test]
    public void ProductWxs_forNonAgentUpManifestContainsOnlyProductBranding()
    {
        var layout = CreateLayoutWithPublishedFiles();
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, "orbitctl.cmd"), "");
        var manifest = OrbitDeskManifest("8F7D9E6B-1B58-4B28-9567-7B09D779B0AC");

        var xml = new WindowsWixSourceGenerator(new WindowsPackageManifest(manifest)).ProductWxs(layout);

        Assert.That(xml, Does.Contain("Name=\"Orbit Desk\""));
        Assert.That(xml, Does.Contain("Manufacturer=\"Orbit Systems\""));
        Assert.That(xml, Does.Contain("Name=\"orbit-desk-core\""));
        Assert.That(xml, Does.Contain("DisplayName=\"Orbit Desk Server\""));
        Assert.That(xml, Does.Contain("Local Orbit Desk runtime authority"));
        Assert.That(xml, Does.Contain("Orbit Desk Installer"));
        Assert.That(xml, Does.Contain("Software\\Orbit Desk"));
        Assert.That(xml, Does.Contain("orbitctl.cmd"));
        Assert.That(xml, Does.Not.Contain("Agent-Up"));
        Assert.That(xml, Does.Not.Contain("agent-up"));
    }

    [Test]
    public void ProductWxs_usesManifestUpgradeGuidSoDistinctProductsCannotUpgradeEachOther()
    {
        var layout = CreateLayoutWithPublishedFiles();
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, "orbitctl.cmd"), "");
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, "nova.cmd"), "");
        var orbit = OrbitDeskManifest("8F7D9E6B-1B58-4B28-9567-7B09D779B0AC");
        var nova = new WindowsInstallerManifest(
            ProductName: "Nova Build",
            Manufacturer: "Nova Labs",
            Version: "1.2.3",
            UpgradeCode: "16357A7B-6C17-4635-85D5-28E74D12F4F3",
            ServiceName: "nova-build-core",
            CliShimName: "nova.cmd",
            BundleName: "Nova Build",
            ServerUrl: "http://127.0.0.1:5002");

        var orbitUpgradeCode = ProductUpgradeCode(new WindowsWixSourceGenerator(new WindowsPackageManifest(orbit)).ProductWxs(layout));
        var novaUpgradeCode = ProductUpgradeCode(new WindowsWixSourceGenerator(new WindowsPackageManifest(nova)).ProductWxs(layout));

        Assert.That(orbitUpgradeCode, Is.EqualTo(orbit.UpgradeCode));
        Assert.That(novaUpgradeCode, Is.EqualTo(nova.UpgradeCode));
        Assert.That(orbitUpgradeCode, Is.Not.EqualTo(novaUpgradeCode));
    }

    [Test]
    public void WindowsPackageManifest_fromNonAgentUpPackageRequestUsesProductManufacturerAndUpgradeGuid()
    {
        var request = new PackageRequest(
            _root,
            "windows",
            "win-x64",
            "1.2.3",
            "artifacts",
            "Release",
            productManifest: new("Orbit Desk", "orbit-desk", "ORBITDESK")
            {
                Manufacturer = "Orbit Systems",
                WindowsUpgradeCode = "8F7D9E6B-1B58-4B28-9567-7B09D779B0AC"
            });

        var manifest = WindowsPackageManifest.From(request).InstallerManifest;

        Assert.That(manifest.ProductName, Is.EqualTo("Orbit Desk"));
        Assert.That(manifest.Manufacturer, Is.EqualTo("Orbit Systems"));
        Assert.That(manifest.ServiceName, Is.EqualTo("orbit-desk-server"));
        Assert.That(manifest.CliShimName, Is.EqualTo("orbit-desk.cmd"));
        Assert.That(manifest.UpgradeCode, Is.EqualTo("8F7D9E6B-1B58-4B28-9567-7B09D779B0AC"));
    }

    [Test]
    public void WindowsPackageManifest_fromAgentUpSlugHonorsExplicitIdentityOverrides()
    {
        var request = new PackageRequest(
            _root,
            "windows",
            "win-x64",
            "1.2.3",
            "artifacts",
            "Release",
            productManifest: new("Agent-Up Fork", "agent-up", "AGENTUPFORK")
            {
                Manufacturer = "Forked Systems",
                WindowsUpgradeCode = "A0D1584E-3E94-4218-B81A-A0D52D3580B7",
                WindowsServiceName = "agent-up-fork-service",
                WindowsCliShimName = "agent-up-fork.cmd",
                WindowsServerUrl = "http://127.0.0.1:5050"
            });

        var manifest = WindowsPackageManifest.From(request).InstallerManifest;

        Assert.That(manifest.ProductName, Is.EqualTo("Agent-Up Fork"));
        Assert.That(manifest.Manufacturer, Is.EqualTo("Forked Systems"));
        Assert.That(manifest.UpgradeCode, Is.EqualTo("A0D1584E-3E94-4218-B81A-A0D52D3580B7"));
        Assert.That(manifest.ServiceName, Is.EqualTo("agent-up-fork-service"));
        Assert.That(manifest.CliShimName, Is.EqualTo("agent-up-fork.cmd"));
        Assert.That(manifest.ServerUrl, Is.EqualTo("http://127.0.0.1:5050"));
    }

    [Test]
    public void BundleAndComponentGuids_areScopedToProductIdentity()
    {
        var layout = CreateLayoutWithPublishedFiles();
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, "orbitctl.cmd"), "");
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, "nova.cmd"), "");
        var orbit = OrbitDeskManifest("8F7D9E6B-1B58-4B28-9567-7B09D779B0AC");
        var nova = new WindowsInstallerManifest(
            ProductName: "Nova Build",
            Manufacturer: "Nova Labs",
            Version: "1.2.3",
            UpgradeCode: "16357A7B-6C17-4635-85D5-28E74D12F4F3",
            ServiceName: "nova-build-core",
            CliShimName: "nova.cmd",
            BundleName: "Nova Build",
            ServerUrl: "http://127.0.0.1:5002");
        var orbitGenerator = new WindowsWixSourceGenerator(new WindowsPackageManifest(orbit));
        var novaGenerator = new WindowsWixSourceGenerator(new WindowsPackageManifest(nova));

        var orbitProduct = orbitGenerator.ProductWxs(layout);
        var novaProduct = novaGenerator.ProductWxs(layout);
        var orbitBundle = orbitGenerator.BundleWxs(layout);
        var novaBundle = novaGenerator.BundleWxs(layout);

        Assert.That(BundleUpgradeCode(orbitBundle), Is.Not.EqualTo(BundleUpgradeCode(novaBundle)));
        Assert.That(ComponentGuids(orbitProduct).Intersect(ComponentGuids(novaProduct)), Is.Empty);
    }

    [TestCaseSource(nameof(ProductIdentityCases))]
    public void ProductWxsAndBundleWxs_forTwoProductsHaveDisjointProductIdentifyingStrings(
        WindowsInstallerManifest first,
        IReadOnlyCollection<string> firstIdentity,
        WindowsInstallerManifest second,
        IReadOnlyCollection<string> secondIdentity)
    {
        var layout = CreateLayoutWithPublishedFiles();
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, first.CliShimName), "");
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, second.CliShimName), "");

        var firstOutput = GeneratedOutput(first, layout);
        var secondOutput = GeneratedOutput(second, layout);

        Assert.Multiple(() =>
        {
            foreach (var value in firstIdentity)
                Assert.That(secondOutput, Does.Not.Contain(value), $"{value} leaked into {second.ProductName}");
            foreach (var value in secondIdentity)
                Assert.That(firstOutput, Does.Not.Contain(value), $"{value} leaked into {first.ProductName}");
        });
    }

    private static IEnumerable<TestCaseData> ProductIdentityCases()
    {
        var agentUp = WindowsInstallerManifest.Create("1.2.3");
        var orbit = OrbitDeskManifest("8F7D9E6B-1B58-4B28-9567-7B09D779B0AC");

        yield return new TestCaseData(
            agentUp,
            new[] { "Agent-Up", "agent-up-server", "agent-up.cmd", agentUp.UpgradeCode },
            orbit,
            new[] { "Orbit Desk", "Orbit Systems", "orbit-desk-core", "orbitctl.cmd", orbit.UpgradeCode })
            .SetName("AgentUp_vs_OrbitDesk");
    }

    private WindowsPackageLayout CreateLayoutWithPublishedFiles()
    {
        var request = new PackageRequest(_root, "windows", "win-x64", "1.2.3", "artifacts", "Release");
        var layout = WindowsPackageLayout.From(request);
        WritePublishedFile(layout.InstallerPublishDirectory, "AgentUp.InstallerApp.exe");
        WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Desktop.exe");
        WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
        WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");
        Directory.CreateDirectory(layout.InstallerSourceDirectory);
        return layout;
    }

    private static WindowsInstallerManifest OrbitDeskManifest(string upgradeCode)
        => new(
            ProductName: "Orbit Desk",
            Manufacturer: "Orbit Systems",
            Version: "1.2.3",
            UpgradeCode: upgradeCode,
            ServiceName: "orbit-desk-core",
            CliShimName: "orbitctl.cmd",
            BundleName: "Orbit Desk",
            ServerUrl: "http://127.0.0.1:5001");

    private static string GeneratedOutput(WindowsInstallerManifest manifest, WindowsPackageLayout layout)
    {
        var generator = new WindowsWixSourceGenerator(new WindowsPackageManifest(manifest));
        return generator.ProductWxs(layout) + Environment.NewLine + generator.BundleWxs(layout);
    }

    private static string? ProductUpgradeCode(string productWxs)
    {
        XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";
        return (string?)XDocument.Parse(productWxs).Descendants(wix + "Package").Single().Attribute("UpgradeCode");
    }

    private static string? BundleUpgradeCode(string bundleWxs)
    {
        XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";
        return (string?)XDocument.Parse(bundleWxs).Descendants(wix + "Bundle").Single().Attribute("UpgradeCode");
    }

    private static IReadOnlyCollection<string> ComponentGuids(string productWxs)
    {
        XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";
        return XDocument.Parse(productWxs)
            .Descendants(wix + "Component")
            .Select(component => (string?)component.Attribute("Guid"))
            .Where(guid => !string.IsNullOrWhiteSpace(guid))
            .Cast<string>()
            .ToArray();
    }

    private static void WritePublishedFile(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, name), "test");
    }
}
