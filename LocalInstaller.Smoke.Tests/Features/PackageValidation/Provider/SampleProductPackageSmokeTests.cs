using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Features.PackageValidation.Providers;
using LocalInstaller.Smoke.Features.PackageValidation.Services;
using LocalInstaller.Smoke.Tests.Features.PackageValidation.Fake;

namespace LocalInstaller.Smoke.Tests.Features.PackageValidation.Provider;

[TestFixture]
public class SampleProductPackageSmokeTests
{
    private static readonly PackageProductManifest AcmeStudio = new(
        ServiceName: "acme-studio-server",
        CliShimName: "acme-studio",
        ArtifactBaseName: "acme-studio",
        DisplayName: "Acme Studio",
        InstallDirName: "Acme Studio",
        WorkspaceConfigFileName: "acme-studio.json",
        InstallerExecutableName: "AcmeStudio.InstallerApp",
        DesktopExecutableName: "AcmeStudio.Desktop",
        ServerExecutableName: "AcmeStudio.Server",
        CliExecutableName: "AcmeStudio.CLI",
        TrayExecutableName: "AcmeStudio.Tray");

    [Test]
    public async Task UbuntuValidator_forAcmeStudio_reportsExpectedPathsAndFindings()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("The Ubuntu package adapter verifies Unix symlinks.");

        var root = Path.Join(Path.GetTempPath(), "AcmeStudio-Smoke-Ubuntu", $"{Guid.NewGuid():N}");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-studio-ubuntu-linux-x64.deb"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.Arguments.Contains("-x"))
                CreateUbuntuRoot(Path.Join(workDir, "root"));
            if (command.Arguments.Contains("-e"))
                CreateUbuntuControl(Path.Join(workDir, "control"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var request = new PackageValidationRequest("ubuntu", "linux-x64", artifactDir, workDir, AcmeStudio);
            var result = await new UbuntuPackageValidator(new UbuntuPackageArchiveProvider(commands)).ValidateAsync(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Is.EqualTo(Path.Join(workDir, "root", "opt", "acme-studio", "installer", "payload", "server", "AcmeStudio.Server")));
            Assert.That(result.CliPath, Is.EqualTo(Path.Join(workDir, "root", "opt", "acme-studio", "installer", "payload", "cli", "AcmeStudio.CLI")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task MacOsValidator_forAcmeStudio_reportsExpectedPathsAndFindings()
    {
        var root = Path.Join(Path.GetTempPath(), "AcmeStudio-Smoke-MacOs", $"{Guid.NewGuid():N}");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-studio-macos-osx-arm64.pkg"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            Assert.That(command.FileName, Is.EqualTo("pkgutil"));
            Assert.That(command.Arguments, Does.Contain("--expand-full"));
            CreateExpandedPackage(Path.Join(workDir, "pkg-expanded"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var request = new PackageValidationRequest("macos", "osx-arm64", artifactDir, workDir, AcmeStudio);
            var result = await new MacOsPackageValidator(new MacOsPackageArchiveProvider(commands)).ValidateAsync(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Does.EndWith(Path.Join("Acme Studio Installer.app", "Contents", "MacOS", "payload", "server", "AcmeStudio.Server")));
            Assert.That(result.CliPath, Does.EndWith(Path.Join("Acme Studio Installer.app", "Contents", "MacOS", "payload", "cli", "AcmeStudio.CLI")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task WindowsValidator_forAcmeStudio_reportsExpectedPathsAndFindings()
    {
        var root = Path.Join(Path.GetTempPath(), "AcmeStudio-Smoke-Windows", $"{Guid.NewGuid():N}");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        Directory.CreateDirectory(artifactDir);
        var installer = Path.Join(artifactDir, "acme-studio-windows-win-x64.exe");
        File.WriteAllText(installer, "");
        File.WriteAllText(Path.Join(artifactDir, "acme-studio-windows-win-x64.msi"), "");
        var commands = new RecordingCommandRunner((command, _) =>
        {
            Assert.That(command.FileName, Is.EqualTo("powershell.exe"));
            Assert.That(command.Environment, Is.Not.Null);
            Assert.That(command.Environment!["AGENTUP_SMOKE_INSTALLER"], Is.EqualTo(installer));
            Assert.That(command.Environment["AGENTUP_SMOKE_LAYOUT"], Is.EqualTo(Path.Join(workDir, "layout")));
            Directory.CreateDirectory(Path.Join(workDir, "layout"));
            return new CommandResult(0, "", "");
        });

        try
        {
            var request = new PackageValidationRequest("windows", "win-x64", artifactDir, workDir, AcmeStudio);
            var result = await new WindowsPackageValidator(new WindowsPackageArchiveProvider(commands)).ValidateAsync(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ServerPath, Is.Null);
            Assert.That(result.CliPath, Is.Null);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateUbuntuRoot(string root)
    {
        WriteExecutable(Path.Join(root, "opt", "acme-studio", "installer", "AcmeStudio.InstallerApp"));
        WriteExecutable(Path.Join(root, "opt", "acme-studio", "installer", "payload", "desktop", "AcmeStudio.Desktop"));
        WriteExecutable(Path.Join(root, "opt", "acme-studio", "installer", "payload", "server", "AcmeStudio.Server"));
        WriteExecutable(Path.Join(root, "opt", "acme-studio", "installer", "payload", "cli", "AcmeStudio.CLI"));
        WriteText(Path.Join(root, "opt", "acme-studio", "installer", "payload", "service", "acme-studio-server.service"),
            "ExecStart=/opt/acme-studio/server/AcmeStudio.Server\nEnvironment=DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/cache/acme-studio\nCacheDirectory=acme-studio\nRestartSec=5\n");
        WriteText(Path.Join(root, "opt", "acme-studio", "installer", "payload", "icon", "Agent-Up.png"), "png");
        WriteText(Path.Join(root, "usr", "share", "applications", "acme-studio-installer.desktop"), "[Desktop Entry]\nName=Acme Studio Installer\n");
        WriteText(Path.Join(root, "usr", "share", "metainfo", "acme-studio-installer.desktop.metainfo.xml"), "<component><id>acme-studio-installer.desktop</id><provides><pkgname>acme-studio</pkgname></provides><releases><release version=\"1.0.0\" date=\"2026-01-01\"/></releases></component>\n");
        WriteText(Path.Join(root, "usr", "share", "pixmaps", "acme-studio.png"), "png");
    }

    private static void CreateUbuntuControl(string control)
    {
        WriteText(Path.Join(control, "postinst"), "#!/usr/bin/env bash\nchmod +x /opt/acme-studio/installer/AcmeStudio.InstallerApp\n");
        WriteText(Path.Join(control, "prerm"), "#!/usr/bin/env bash\n");
    }

    private static void CreateExpandedPackage(string root)
    {
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Acme Studio Installer.app", "Contents", "MacOS", "AcmeStudio.InstallerApp"));
        WriteText(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Acme Studio Installer.app", "Contents", "Info.plist"), "CFBundleIconFile\nAcme-Studio.png\n");
        WriteText(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Acme Studio Installer.app", "Contents", "Resources", "Acme-Studio.png"), "");
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Acme Studio Installer.app", "Contents", "MacOS", "payload", "desktop", "AcmeStudio.Desktop"));
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Acme Studio Installer.app", "Contents", "MacOS", "payload", "server", "AcmeStudio.Server"));
        WriteExecutable(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Acme Studio Installer.app", "Contents", "MacOS", "payload", "cli", "AcmeStudio.CLI"));
        WriteText(Path.Join(root, "InstallerApp.pkg", "Payload", "Applications", "Acme Studio Installer.app", "Contents", "MacOS", "payload", "icon", "Acme-Studio.png"), "");
        WriteText(Path.Join(root, "InstallerApp.pkg", "Scripts", "postinstall"), "open -a \"/Applications/Acme Studio Installer.app\"\n");
        WriteText(Path.Join(root, "Distribution"), "InstallerApp.pkg\n");
    }

    private static void WriteExecutable(string path)
    {
        WriteText(path, "");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
