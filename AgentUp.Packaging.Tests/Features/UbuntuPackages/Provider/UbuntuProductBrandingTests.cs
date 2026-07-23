using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Providers;
using AgentUp.Packaging.Features.UbuntuPackages.Services;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Tests.Features.UbuntuPackages.Provider;

[TestFixture]
public class UbuntuProductBrandingTests
{
    private static readonly PackageProductManifest AcmeStudio = new("Acme Studio", "acme-studio", "ACMESTUDIO");

    // Test 1: control file for a non-Agent-Up manifest carries the product's package name
    // throughout, and grepping all generated packaging artifacts for "agent-up" returns zero matches.
    [Test]
    public void ControlFile_forNonAgentUpProduct_carriesProductPackageName_andNoArtifactContainsAgentUpString()
    {
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.0.0", "artifacts", "Release");
        var manifest = UbuntuPackageManifest.From(request, AcmeStudio);
        var layout = UbuntuPackageLayout.From(request, AcmeStudio);
        var writer = new RecordingPackageWriter();

        new UbuntuPackageStager(writer).Stage(request, layout, manifest);

        Assert.That(manifest.ControlFileText(), Does.Contain("Package: acme-studio"),
            "control file must carry the product's package name");

        foreach (var (path, text) in writer.WrittenText)
            Assert.That(text, Does.Not.Contain("agent-up"),
                $"artifact written to '{path}' must not contain 'agent-up'");

        foreach (var path in writer.AllPaths)
            Assert.That(path, Does.Not.Contain("agent-up"),
                $"staged path '{path}' must not contain 'agent-up'");
    }

    // Test 2: generated post-install script invokes the installer under the product's install root
    // with --install-core, with no reference to /opt/agent-up.
    [Test]
    public void PostInstallScript_forNonAgentUpProduct_usesProductInstallRootAndUnitName()
    {
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.0.0", "artifacts", "Release");
        var manifest = UbuntuPackageManifest.From(request, AcmeStudio);

        var script = manifest.PostInstallScript();

        Assert.That(script, Does.Contain("/opt/acme-studio"),
            "post-install script must reference the product's install root");
        Assert.That(script, Does.Contain("SUDO_USER"),
            "post-install script must attempt to launch the installer GUI for the invoking user");
        Assert.That(script, Does.Not.Contain("--install-core"),
            "post-install script must not run a headless install");
        Assert.That(script, Does.Not.Contain("/opt/agent-up"),
            "post-install script must not reference /opt/agent-up");
    }

    // Test 3: generated pre-remove script uninstalls each component via the product's installer,
    // and an "acme-studio" pre-remove script contains no "agent-up" string.
    [Test]
    public void PreRemoveScript_forAcmeStudio_stopsProductUnitAndContainsNoAgentUpUnitName()
    {
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.0.0", "artifacts", "Release");
        var manifest = UbuntuPackageManifest.From(request, AcmeStudio);

        var script = manifest.PreRemoveScript();

        Assert.That(script, Does.Contain("/opt/acme-studio/installer"),
            "pre-remove script must invoke the product's installer");
        Assert.That(script, Does.Contain("--uninstall-component"),
            "pre-remove script must uninstall each component");
        Assert.That(script, Does.Not.Contain("agent-up"),
            "pre-remove script for acme-studio must not contain any 'agent-up' string");
    }

    // Test 4: the exact dpkg-deb command shape produced for Agent-Up's manifest is
    // byte-for-byte identical to the current baseline — no regression in Agent-Up packaging output.
    [Test]
    public async Task BuildDebAsync_forAgentUpManifest_commandShapeMatchesBaseline()
    {
        var commands = new RecordingCommandRunner();
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "out", "Release");
        var layout = UbuntuPackageLayout.From(request, PackageProductManifest.AgentUp());

        await new DpkgDebPackageTool(commands).BuildDebAsync(layout);

        Assert.That(commands.Commands.Single().FileName, Is.EqualTo("dpkg-deb"));
        Assert.That(commands.Commands.Single().Arguments, Is.EqualTo(new[]
        {
            "--build",
            Path.Join("/repo", "artifacts", "stage", "ubuntu-linux-x64", "deb-root"),
            Path.Join("/repo", "out", "agent-up-ubuntu-linux-x64.deb")
        }));
    }

    // Test 5: theory — Agent-Up and a second product produce .deb artifacts with disjoint
    // sets of product-identifying strings. No string unique to one product appears in the other's output.
    [TestCase("agent-up", "acme-studio")]
    [TestCase("agent-up", "my-tool")]
    public void ProductArtifacts_twoProducts_haveDisjointProductIdentifyingStrings(
        string firstSlug, string secondSlug)
    {
        var firstOutput = CollectAllArtifactOutput(firstSlug);
        var secondOutput = CollectAllArtifactOutput(secondSlug);

        Assert.That(secondOutput, Does.Not.Contain(firstSlug),
            $"Second product's artifacts must not contain '{firstSlug}'");
        Assert.That(firstOutput, Does.Not.Contain(secondSlug),
            $"First product's artifacts must not contain '{secondSlug}'");
    }

    private static string CollectAllArtifactOutput(string slug)
    {
        var product = new PackageProductManifest(SlugToProductName(slug), slug, slug.ToUpperInvariant().Replace("-", ""));
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.0.0", "artifacts", "Release");
        var manifest = UbuntuPackageManifest.From(request, product);
        var layout = UbuntuPackageLayout.From(request, product);
        var writer = new RecordingPackageWriter();

        new UbuntuPackageStager(writer).Stage(request, layout, manifest);

        return string.Concat(writer.WrittenText.Values) + string.Concat(writer.AllPaths);
    }

    private static string SlugToProductName(string slug)
        => string.Concat(slug.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private sealed class RecordingPackageWriter : IPackageWriter
    {
        public Dictionary<string, string> WrittenText { get; } = [];
        public List<string> AllPaths { get; } = [];

        public void ResetDirectory(string path) => AllPaths.Add(path);
        public void CreateDirectory(string path) => AllPaths.Add(path);
        public void CopyDirectory(string source, string destination) => AllPaths.Add(destination);
        public void CopyFile(string source, string destination) => AllPaths.Add(destination);
        public void WriteText(string path, string text) { AllPaths.Add(path); WrittenText[path] = text; }
        public void CreateSymbolicLink(string linkPath, string targetPath) => AllPaths.Add(linkPath);
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
