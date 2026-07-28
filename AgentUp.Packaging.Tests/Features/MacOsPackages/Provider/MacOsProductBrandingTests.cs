using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Providers;
using AgentUp.Packaging.Features.MacOsPackages.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages.Provider;

[TestFixture]
public class MacOsProductBrandingTests
{
    private static readonly PackageProductManifest AcmeStudio = new("Acme Studio", "acme-studio", "ACMESTUDIO");

    // Test 1: distribution XML for a non-Agent-Up manifest carries the product's title and bundle
    // identifier throughout, and grepping all generated packaging artifacts for "Agent-Up" returns zero matches.
    [Test]
    public void DistributionXml_forNonAgentUpProduct_carriesProductTitleAndBundleId_andNoArtifactContainsAgentUpString()
    {
        var request = new PackageRequest("/repo", "macos", "osx-arm64", "1.0.0", "artifacts", "Release",
            productManifest: AcmeStudio);
        var layout = MacOsPackageLayout.From(request);
        var manifest = MacOsPackageManifest.From(request);
        var writer = new RecordingMacOsPackageWriter();

        new MacOsPackageStager(writer).Stage(layout, manifest);

        var xml = writer.WrittenText[layout.DistributionXmlPath];
        Assert.That(xml, Does.Contain("Acme Studio"),
            "distribution XML must carry the product's title");
        Assert.That(xml, Does.Contain("dev.acme-studio.installer"),
            "distribution XML must carry the product's bundle identifier");

        foreach (var (path, text) in writer.WrittenText)
            Assert.That(text, Does.Not.Contain("Agent-Up"),
                $"artifact at '{path}' must not contain 'Agent-Up'");
    }

    // Test 2: generated post-install script opens the product's app bundle by the product's app name,
    // with no reference to "Agent-Up Installer.app".
    [Test]
    public void PostInstallScript_forNonAgentUpProduct_opensProductAppBundle_withNoReferenceToAgentUpInstallerApp()
    {
        var request = new PackageRequest("/repo", "macos", "osx-arm64", "1.0.0", "artifacts", "Release",
            productManifest: AcmeStudio);
        var manifest = MacOsPackageManifest.From(request);
        var installerAppName = $"{manifest.InstallerManifest.ProductName} Installer";
        var logDirectory = $"/Library/Logs/{manifest.InstallerManifest.ProductName}";

        var script = MacOsScriptGenerator.InstallerPostInstallScript(installerAppName, logDirectory);

        Assert.That(script, Does.Contain("open -a \"/Applications/Acme Studio Installer.app\""),
            "post-install script must open the product's installer app bundle");
        Assert.That(script, Does.Not.Contain("Agent-Up Installer.app"),
            "post-install script must not reference 'Agent-Up Installer.app'");
    }

    // Test 3: installer startup error log path in the generated script is derived from the product's name,
    // with no reference to Agent-Up's log directory.
    [Test]
    public void PostInstallScript_forNonAgentUpProduct_logPathDerivedFromProductName_withNoReferenceToAgentUpLogDir()
    {
        var request = new PackageRequest("/repo", "macos", "osx-arm64", "1.0.0", "artifacts", "Release",
            productManifest: AcmeStudio);
        var manifest = MacOsPackageManifest.From(request);
        var installerAppName = $"{manifest.InstallerManifest.ProductName} Installer";
        var logDirectory = $"/Library/Logs/{manifest.InstallerManifest.ProductName}";

        var script = MacOsScriptGenerator.InstallerPostInstallScript(installerAppName, logDirectory);

        Assert.That(script, Does.Contain("/Library/Logs/Acme Studio"),
            "installer startup error log path must be derived from the product's name");
        Assert.That(script, Does.Not.Contain("/Library/Logs/Agent-Up"),
            "log path must not reference Agent-Up's log directory");
    }

    // Test 4: the exact pkgbuild and productbuild command shapes produced for Agent-Up's manifest are
    // byte-for-byte identical to the current baseline — no regression in Agent-Up packaging output.
    [Test]
    public async Task BuildComponentAndProductPackage_forAgentUpManifest_commandShapeMatchesBaseline()
    {
        var commands = new RecordingCommandRunner();
        var request = new PackageRequest("/repo", "macos", "osx-arm64", "1.2.3", "out", "Release");
        var layout = MacOsPackageLayout.From(request);
        var manifest = MacOsPackageManifest.From(request);
        var tool = new MacOsPackageTool(commands);

        await tool.BuildComponentPackagesAsync(request, layout, manifest);
        await tool.BuildProductPackageAsync(layout);

        var stage = request.StageDirectory;
        Assert.That(commands.Commands[0].FileName, Is.EqualTo("pkgbuild"));
        Assert.That(commands.Commands[0].Arguments, Is.EqualTo(new[]
        {
            "--identifier", "dev.agent-up.installer",
            "--version", "1.2.3",
            "--root", Path.Join(stage, "pkg-root", "installer"),
            "--scripts", Path.Join(stage, "installer-pkg-scripts"),
            "--install-location", "/",
            Path.Join(stage, "component-packages", "InstallerApp.pkg")
        }));
        Assert.That(commands.Commands[1].FileName, Is.EqualTo("productbuild"));
        Assert.That(commands.Commands[1].Arguments, Is.EqualTo(new[]
        {
            "--distribution", Path.Join(stage, "Distribution.xml"),
            "--package-path", Path.Join(stage, "component-packages"),
            Path.Join("/repo", "out", "agent-up-macos-osx-arm64.pkg")
        }));
    }

    // Test 5: theory — Agent-Up and a second product produce .pkg artifacts with disjoint sets of
    // product-identifying strings. No string from one product's output appears in the other's.
    [TestCase("agent-up", "acme-studio")]
    [TestCase("agent-up", "my-tool")]
    public void ProductArtifacts_twoProducts_haveDisjointProductIdentifyingStrings(
        string firstSlug, string secondSlug)
    {
        var firstOutput = CollectAllArtifactOutput(firstSlug);
        var secondOutput = CollectAllArtifactOutput(secondSlug);

        Assert.That(secondOutput, Does.Not.Contain(firstSlug),
            $"second product's artifacts must not contain '{firstSlug}'");
        Assert.That(firstOutput, Does.Not.Contain(secondSlug),
            $"first product's artifacts must not contain '{secondSlug}'");
    }

    private static string CollectAllArtifactOutput(string slug)
    {
        var product = new PackageProductManifest(SlugToProductName(slug), slug, slug.ToUpperInvariant().Replace("-", ""));
        var request = new PackageRequest("/repo", "macos", "osx-arm64", "1.0.0", "artifacts", "Release",
            productManifest: product);
        var layout = MacOsPackageLayout.From(request);
        var manifest = MacOsPackageManifest.From(request);
        var writer = new RecordingMacOsPackageWriter();

        new MacOsPackageStager(writer).Stage(layout, manifest);

        return string.Concat(writer.WrittenText.Values) + string.Concat(writer.AllPaths);
    }

    private static string SlugToProductName(string slug)
        => string.Concat(slug.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private sealed class RecordingMacOsPackageWriter : IMacOsPackageWriter
    {
        public Dictionary<string, string> WrittenText { get; } = [];
        public List<string> AllPaths { get; } = [];

        public void ResetDirectory(string path) => AllPaths.Add(path);
        public void CreateDirectory(string path) => AllPaths.Add(path);
        public void CopyDirectory(string source, string destination) => AllPaths.Add(destination);
        public void CopyFile(string source, string destination) => AllPaths.Add(destination);
        public void WriteText(string path, string text) { AllPaths.Add(path); WrittenText[path] = text; }
        public void SetExecutable(string path) => AllPaths.Add(path);
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<CommandSpec> Commands { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(new CommandResult(0, "", ""));
        }
    }
}
