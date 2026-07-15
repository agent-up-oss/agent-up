namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts;

[TestFixture]
public class NixPackagingWrapperTests
{
    private static readonly string Root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);

    [TestCase("ubuntu", "dpkg", "fakeroot")]
    [TestCase("windows", "msitools", "osslsigncode")]
    [TestCase("macos", "hdiutil", "pkgbuild")]
    public void NixShell_declaresPlatformPackagingEnvironment(string platform, string requiredText, string expectedDiagnosticOrTool)
    {
        var nixFile = Path.Combine(Root, "packaging", "nix", $"{platform}-package.nix");

        var text = File.ReadAllText(nixFile);

        Assert.That(text, Does.Contain($"AGENTUP_PACKAGING_TARGET={platform}"));
        Assert.That(text, Does.Contain(requiredText));
        Assert.That(text, Does.Contain(expectedDiagnosticOrTool));
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
        Assert.That(text, Does.Contain($"package-release.sh\" {platform}"));
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
