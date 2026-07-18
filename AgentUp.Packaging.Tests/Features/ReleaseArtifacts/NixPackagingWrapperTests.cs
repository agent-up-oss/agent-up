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
        var nixFile = Path.Join(Root, "packaging", "nix", $"{platform}-package.nix");

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
        var script = Path.Join(Root, "scripts", $"package-{platform}.sh");

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
        var script = Path.Join(Root, "scripts", "package-release.sh");

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

    [Test]
    public void PackageRelease_generatesNixModulesThatRegisterAgentUpServerService()
    {
        var script = Path.Join(Root, "scripts", "package-release.sh");

        var text = File.ReadAllText(script);

        Assert.That(text, Does.Contain("systemd.services.agent-up-server"));
        Assert.That(text, Does.Not.Contain("systemd.services.agent-up ="));
        Assert.That(text, Does.Contain("systemd.user.services.agent-up-server"));
        Assert.That(text, Does.Contain("cfg.server.enable"));
        Assert.That(text, Does.Contain("default = \"${config.xdg.stateHome}/agent-up\""));
        Assert.That(text, Does.Contain("ExecStart = \"${package}/bin/agent-up-server --urls http://127.0.0.1:${toString cfg.port}\""));
        Assert.That(text, Does.Contain("ExecStart = \"${package}/bin/agent-up-server --urls http://127.0.0.1:${toString cfg.server.port}\""));
    }

    [Test]
    public void PackageRelease_generatesNixCapabilityDeclarations()
    {
        var script = Path.Join(Root, "scripts", "package-release.sh");

        var text = File.ReadAllText(script);

        Assert.That(text, Does.Contain("options.services.agent-up"));
        Assert.That(text, Does.Contain("capabilities = lib.mkOption"));
        Assert.That(text, Does.Contain("example = { dotnet = [ \"10.0.x\" ]; docker = [ \"27.x\" ]; };"));
        Assert.That(text, Does.Contain("environment.etc.\"agent-up/capabilities.json\".text = capabilityInventory"));
        Assert.That(text, Does.Contain("home.file.\".config/agent-up/capabilities.json\".text = capabilityInventory"));
        Assert.That(text, Does.Contain("AGENTUP_CAPABILITY_INVENTORY_PATH"));
    }

    [Test]
    public void PackageRelease_generatesNixInstallerAppLauncherInLookupOnlyMode()
    {
        var script = Path.Join(Root, "scripts", "package-release.sh");

        var text = File.ReadAllText(script);

        Assert.That(text, Does.Contain("AgentUp.InstallerApp/AgentUp.InstallerApp.csproj"));
        Assert.That(text, Does.Contain("cp -a \"$stage/installer\" \"$pkgs_root/package/opt/agent-up/installer\""));
        Assert.That(text, Does.Contain("ln -s $out/opt/agent-up/installer/AgentUp.InstallerApp $out/bin/agent-up-installer"));
        Assert.That(text, Does.Contain("--set AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY 1"));
        Assert.That(text, Does.Contain("xdg.desktopEntries.agent-up-installer"));
    }

    [Test]
    public void InstallerApp_declaresNixOsLaunchProfile()
    {
        var launchSettings = Path.Join(Root, "AgentUp.InstallerApp", "Properties", "launchSettings.json");
        var runScript = Path.Join(Root, "run-installer.sh");

        Assert.That(File.ReadAllText(launchSettings), Does.Contain("AgentUp.InstallerApp (NixOS)"));
        Assert.That(File.ReadAllText(launchSettings), Does.Contain("AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY"));
        Assert.That(File.ReadAllText(runScript), Does.Contain("AgentUp.InstallerApp/AgentUp.InstallerApp.csproj"));
        Assert.That(File.ReadAllText(runScript), Does.Contain("nix-shell"));
    }

    [Test]
    public void CiPackageSmoke_validatesGeneratedNixServiceRegistration()
    {
        var script = Path.Join(Root, ".github", "scripts", "smoke-package.sh");

        var text = File.ReadAllText(script);

        Assert.That(text, Does.Contain("options.programs.agent-up ="));
        Assert.That(text, Does.Not.Contain("options.programs.agent-up.enable"));
        Assert.That(text, Does.Contain("systemd.services.agent-up-server"));
        Assert.That(text, Does.Contain("systemd.user.services.agent-up-server"));
        Assert.That(text, Does.Contain("cfg.server.enable"));
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
