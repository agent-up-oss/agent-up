namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts;

[TestFixture]
public class NixPackagingWrapperTests
{
    private static readonly string Root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);

    [TestCase("ubuntu", "dpkg", "fakeroot")]
    [TestCase("windows", "dotnet tool restore", "dotnet-tools.json")]
    [TestCase("macos", "hdiutil", "pkgbuild")]
    public void NixShell_declaresPlatformPackagingEnvironment(string platform, string requiredText, string expectedDiagnosticOrTool)
    {
        var nixFile = Path.Combine(Root, "packaging", "nix", $"{platform}-package.nix");

        var text = File.ReadAllText(nixFile);

        Assert.That(text, Does.Contain($"AGENTUP_PACKAGING_TARGET={platform}"));
        Assert.That(text, Does.Contain(requiredText));
        Assert.That(text, Does.Contain(expectedDiagnosticOrTool));
        if (platform == "windows")
        {
            Assert.That(text, Does.Contain("cd \"$AGENTUP_REPO_ROOT/packaging/windows\""));
            Assert.That(text, Does.Contain("dotnet tool run wix"));
        }
    }

    [TestCase("ubuntu")]
    [TestCase("windows")]
    [TestCase("macos")]
    public void Wrapper_entersNixShellBeforeDelegatingToPackageRelease(string platform)
    {
        var script = Path.Combine(Root, "scripts", $"package-{platform}.sh");

        var text = File.ReadAllText(script);

        Assert.That(text, Does.Contain($"AGENTUP_PACKAGING_TARGET:-}}\" != \"{platform}\""));
        Assert.That(text, Does.Contain($"packaging/nix/{platform}-package.nix"));
        Assert.That(text, Does.Contain("printf \"%q \" \"$0\" \"$@\""));
        if (platform is "windows" or "macos")
        {
            Assert.That(text, Does.Contain("AgentUp.Packaging.csproj"));
            Assert.That(text, Does.Contain($"package {platform}"));
            if (platform == "windows")
                Assert.That(text, Does.Contain("command -v wix"));
        }
        else
        {
            Assert.That(text, Does.Contain($"package-release.sh\" {platform}"));
        }
    }

    [Test]
    public void PackageRelease_delegatesWindowsToDotNetPackagingAndDoesNotGenerateLegacyInstaller()
    {
        var script = Path.Combine(Root, "scripts", "package-release.sh");

        var text = File.ReadAllText(script);

        Assert.That(text, Does.Contain("[ \"$platform\" = \"ubuntu\" ] || [ \"$platform\" = \"macos\" ] || [ \"$platform\" = \"windows\" ]"));
        Assert.That(text, Does.Contain("ensure_wix_cli"));
        Assert.That(text, Does.Contain("AgentUp.Packaging/AgentUp.Packaging.csproj"));
        Assert.That(text, Does.Not.Contain("create_windows_installer"));
        Assert.That(text, Does.Not.Contain("UseWindowsForms"));
        Assert.That(text, Does.Not.Contain("payload.zip"));
        Assert.That(text, Does.Not.Contain("--extract"));
        Assert.That(text, Does.Not.Contain("--install-dir"));
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find repository root from {startDirectory}.");
    }
}
