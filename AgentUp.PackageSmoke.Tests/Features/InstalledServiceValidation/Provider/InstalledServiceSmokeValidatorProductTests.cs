using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Fake;
using AgentUp.PackageSmoke.Tests.Features.PackageValidation.Fake;
using AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity.Fake;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation.Provider;

[TestFixture]
public sealed class InstalledServiceSmokeValidatorProductTests
{
    private static readonly InstalledServiceProductManifest AcmeProduct = new(
        ServiceName: "acme-server",
        CliShimName: "acme",
        ArtifactBaseName: "acme",
        DisplayName: "Acme",
        InstallDirName: "Acme",
        WorkspaceConfigFileName: "acme.json");

    [Test]
    public async Task ValidateAsync_acmeProduct_doesNotProbeAgentUpServiceOrCliNames()
    {
        var root = TempRoot("product-isolation");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        var systemRoot = Path.Join(root, "system");
        SetupUbuntuSystemFiles(systemRoot, "acme");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-ubuntu-linux-x64.deb"), "");

        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (IsUnixCliCommand(command, "acme", "start"))
                return new CommandResult(0, "Started workspace \"Installed Service Smoke Workspace\"", "");
            if (IsUnixCliCommand(command, "acme", "status"))
                return new CommandResult(0, "Name:       Installed Service Smoke Workspace\nState:      Running\n", "");
            if (command.FileName == "sudo" && command.Arguments.SequenceEqual(["apt-get", "purge", "-y", "acme"]))
            {
                RemoveUbuntuSystemFiles(systemRoot, "acme");
                return new CommandResult(0, "", "");
            }
            if (command.FileName == "sudo" && command.Arguments.SequenceEqual(["systemctl", "status", "acme-server.service", "--no-pager"]))
                return new CommandResult(1, "", "Unit acme-server.service could not be found.");
            if (command.FileName == "bash" && command.Arguments.Any(argument => argument.Contains("command -v acme", StringComparison.Ordinal)))
                return File.Exists(Path.Join(systemRoot, "usr", "share", "applications", "acme.desktop"))
                    ? new CommandResult(0, "/usr/bin/acme", "")
                    : new CommandResult(1, "", "acme: not found");
            return new CommandResult(0, "", "");
        });
        var probe = new FakeServerProbe("http://127.0.0.1:5000");
        var previousSkip = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL");

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", "1");
            var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", artifactDir, workDir,
                ProductConfig: AcmeProduct, SystemRoot: systemRoot);
            using var validator = new UbuntuInstalledServiceSmokeValidator(commands, probe, new NullRuntimeSecurityChecks(),
                new HttpClient(new FakeTraySessionHttpHandler()));
            var result = await validator.ValidateAsync(request);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Findings.Select(f => f.Message)));

            var allArguments = commands.Commands
                .SelectMany(c => c.Arguments.Prepend(c.FileName))
                .ToList();

            Assert.That(allArguments.Any(a => a.Contains("agent-up", StringComparison.Ordinal)), Is.False,
                "No command argument should reference 'agent-up' when running an Acme smoke");
            Assert.That(allArguments.Any(a => a.Contains("acme", StringComparison.Ordinal)), Is.True,
                "Commands should reference the Acme CLI shim name");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", previousSkip);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_acmeProduct_serviceNotFoundFindingNamesAcmeService()
    {
        var root = TempRoot("product-service-finding");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        var systemRoot = Path.Join(root, "system");
        SetupUbuntuSystemFiles(systemRoot, "acme");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-ubuntu-linux-x64.deb"), "");

        var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", artifactDir, workDir,
            ProductConfig: AcmeProduct, SystemRoot: systemRoot);

        try
        {
            var result = await new UbuntuInstalledServiceSmokeValidator(
                    new RecordingCommandRunner((_, _) => new CommandResult(0, "", "")),
                    new FakeServerProbe(null),
                    new NullRuntimeSecurityChecks())
                .ValidateAsync(request);

            var finding = result.Findings.Single(f => f.Code == "installed.server.ready");
            Assert.That(finding.Message, Does.Contain("acme-server"),
                "Finding message should name the Acme product's service");
            Assert.That(finding.Message, Does.Not.Contain("agent-up"),
                "Finding message must not reference agent-up");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_acmeProductAgainstAgentUpInstall_reportsAcmeServiceAndCliMissing()
    {
        var root = TempRoot("product-agentup-install-mismatch");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        var systemRoot = Path.Join(root, "system");
        SetupUbuntuSystemFiles(systemRoot, "agent-up");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-ubuntu-linux-x64.deb"), "");

        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.FileName == "bash" && command.Arguments.Any(argument => argument.Contains("command -v acme", StringComparison.Ordinal)))
                return new CommandResult(1, "", "acme: not found");
            if (command.FileName == "acme")
                return new CommandResult(1, "", "acme: not found");
            if (command.FileName == "sudo" && command.Arguments.SequenceEqual(["systemctl", "status", "acme-server.service", "--no-pager"]))
                return new CommandResult(1, "", "Unit acme-server.service could not be found.");
            return new CommandResult(0, "", "");
        });
        var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", artifactDir, workDir,
            ProductConfig: AcmeProduct, SystemRoot: systemRoot);

        try
        {
            var result = await new UbuntuInstalledServiceSmokeValidator(
                    commands,
                    new FakeServerProbe(null),
                    new NullRuntimeSecurityChecks())
                .ValidateAsync(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Findings.Any(finding =>
                finding.Code == "installed.ubuntu.path" &&
                finding.Message.Contains("acme: not found", StringComparison.Ordinal)), Is.True);
            Assert.That(result.Findings.Any(finding =>
                finding.Code == "installed.server.ready" &&
                finding.Message.Contains("acme-server", StringComparison.Ordinal)), Is.True);
            Assert.That(result.Findings.Where(finding => finding.Code is "installed.ubuntu.path" or "installed.server.ready")
                .Select(finding => finding.Message), Has.None.Contain("agent-up"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_acmeInstalledFileChecksIgnoreAgentUpInstallRoot()
    {
        var root = TempRoot("product-install-root-isolation");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        var systemRoot = Path.Join(root, "system");
        SetupUbuntuSystemFiles(systemRoot, "agent-up");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-ubuntu-linux-x64.deb"), "");

        var commands = new RecordingCommandRunner((command, _) =>
            command.FileName == "bash" && command.Arguments.Any(argument => argument.Contains("command -v acme", StringComparison.Ordinal))
                ? new CommandResult(1, "", "acme: not found")
                : new CommandResult(0, "", ""));
        var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", artifactDir, workDir,
            ProductConfig: AcmeProduct, SystemRoot: systemRoot);

        try
        {
            var result = await new UbuntuInstalledServiceSmokeValidator(
                    commands,
                    new FakeServerProbe(null),
                    new NullRuntimeSecurityChecks())
                .ValidateAsync(request);

            var missingFiles = result.Findings
                .Where(finding => finding.Message.StartsWith("Expected file missing:", StringComparison.Ordinal))
                .Select(finding => finding.Message)
                .ToArray();

            Assert.That(missingFiles, Is.Not.Empty);
            Assert.That(missingFiles, Has.Some.Contain(Path.Join(systemRoot, "opt", "acme")));
            Assert.That(missingFiles, Has.None.Contain(Path.Join(systemRoot, "opt", "agent-up")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_acmeUninstallChecksOnlyAcmeTargets()
    {
        var root = TempRoot("product-uninstall-isolation");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        var systemRoot = Path.Join(root, "system");
        SetupUbuntuSystemFiles(systemRoot, "acme");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-ubuntu-linux-x64.deb"), "");

        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (IsUnixCliCommand(command, "acme", "start"))
                return new CommandResult(0, "Started workspace \"Installed Service Smoke Workspace\"", "");
            if (IsUnixCliCommand(command, "acme", "status"))
                return new CommandResult(0, "Name:       Installed Service Smoke Workspace\nState:      Running\n", "");
            if (command.FileName == "sudo" && command.Arguments.SequenceEqual(["systemctl", "status", "acme-server.service", "--no-pager"]))
                return new CommandResult(1, "", "Unit acme-server.service could not be found.");
            if (command.FileName == "bash" && command.Arguments.Any(argument => argument.Contains("command -v acme", StringComparison.Ordinal)))
                return new CommandResult(1, "", "acme: not found");
            return new CommandResult(0, "", "");
        });
        var previousSkip = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL");

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", "1");
            var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", artifactDir, workDir,
                ProductConfig: AcmeProduct, SystemRoot: systemRoot);
            using var validator = new UbuntuInstalledServiceSmokeValidator(commands, new FakeServerProbe("http://127.0.0.1:5000"),
                new NullRuntimeSecurityChecks(), new HttpClient(new FakeTraySessionHttpHandler()));
            await validator.ValidateAsync(request);

            var allArguments = commands.Commands.SelectMany(command => command.Arguments.Prepend(command.FileName)).ToArray();
            Assert.That(commands.Commands.Any(command =>
                command.FileName == "sudo" && command.Arguments.SequenceEqual(["apt-get", "purge", "-y", "acme"])), Is.True);
            Assert.That(commands.Commands.Any(command =>
                command.FileName == "sudo" && command.Arguments.SequenceEqual(["systemctl", "status", "acme-server.service", "--no-pager"])), Is.True);
            Assert.That(commands.Commands.Any(command =>
                command.FileName == "bash" && command.Arguments.Any(argument => argument.Contains("command -v acme", StringComparison.Ordinal))), Is.True);
            Assert.That(allArguments.Any(argument => argument.Contains("agent-up", StringComparison.Ordinal)), Is.False);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", previousSkip);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_agentUpAndAcmeRunsInSameProcessHaveIndependentFindings()
    {
        var root = TempRoot("product-finding-isolation");
        var artifactDir = Path.Join(root, "artifacts");
        var agentWorkDir = Path.Join(root, "agent-work");
        var acmeWorkDir = Path.Join(root, "acme-work");
        var agentSystemRoot = Path.Join(root, "agent-system");
        var acmeSystemRoot = Path.Join(root, "acme-system");
        SetupUbuntuSystemFiles(agentSystemRoot, "agent-up");
        SetupUbuntuSystemFiles(acmeSystemRoot, "acme");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "agent-up-ubuntu-linux-x64.deb"), "");
        File.WriteAllText(Path.Join(artifactDir, "acme-ubuntu-linux-x64.deb"), "");

        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (command.FileName == "agent-up" || command.FileName == "acme")
                return new CommandResult(1, "", $"{command.FileName}: not found");
            if (command.FileName == "bash" && command.Arguments.Any(argument => argument.Contains("command -v", StringComparison.Ordinal)))
                return new CommandResult(1, "", "not found");
            if (command.FileName == "sudo" && command.Arguments.Any(argument => argument.EndsWith(".service", StringComparison.Ordinal)))
                return new CommandResult(1, "", "service not found");
            return new CommandResult(0, "", "");
        });

        try
        {
            using var validator = new UbuntuInstalledServiceSmokeValidator(commands, new FakeServerProbe(null), new NullRuntimeSecurityChecks());
            var agentResult = await validator.ValidateAsync(new InstalledServiceSmokeRequest(
                "ubuntu", "linux-x64", artifactDir, agentWorkDir, SystemRoot: agentSystemRoot));
            var acmeResult = await validator.ValidateAsync(new InstalledServiceSmokeRequest(
                "ubuntu", "linux-x64", artifactDir, acmeWorkDir, ProductConfig: AcmeProduct, SystemRoot: acmeSystemRoot));

            Assert.That(agentResult.Findings, Is.Not.SameAs(acmeResult.Findings));
            Assert.That(agentResult.Findings.Select(finding => finding.Message), Has.Some.Contain("agent-up-server"));
            Assert.That(agentResult.Findings.Select(finding => finding.Message), Has.None.Contain("acme-server"));
            Assert.That(acmeResult.Findings.Select(finding => finding.Message), Has.Some.Contain("acme-server"));
            Assert.That(acmeResult.Findings.Select(finding => finding.Message), Has.None.Contain("agent-up-server"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_acmeProduct_cliReachabilityProbeUsesAcmeShimName()
    {
        var root = TempRoot("product-cli-probe");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        var systemRoot = Path.Join(root, "system");
        SetupUbuntuSystemFiles(systemRoot, "acme");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "acme-ubuntu-linux-x64.deb"), "");

        var commands = new RecordingCommandRunner((_, _) => new CommandResult(0, "", ""));
        var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", artifactDir, workDir,
            ProductConfig: AcmeProduct, SystemRoot: systemRoot);

        try
        {
            await new UbuntuInstalledServiceSmokeValidator(commands, new FakeServerProbe(null), new NullRuntimeSecurityChecks())
                .ValidateAsync(request);

            var cliProbe = commands.Commands.FirstOrDefault(c =>
                c.FileName == "bash" && c.Arguments.Any(a => a.Contains("command -v", StringComparison.Ordinal)));

            Assert.That(cliProbe, Is.Not.Null, "A CLI reachability probe should have been issued");
            var probeArg = cliProbe!.Arguments.First(a => a.Contains("command -v", StringComparison.Ordinal));
            Assert.That(probeArg, Does.Contain("acme"),
                "CLI probe should check for 'acme'");
            Assert.That(probeArg, Does.Not.Contain("agent-up"),
                "CLI probe must not check for 'agent-up' when product is Acme");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ValidateAsync_agentUpProduct_producesBaselineServiceAndCliFindings()
    {
        var root = TempRoot("product-agentup-baseline");
        var artifactDir = Path.Join(root, "artifacts");
        var workDir = Path.Join(root, "work");
        var systemRoot = Path.Join(root, "system");
        SetupUbuntuSystemFiles(systemRoot, "agent-up");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Join(artifactDir, "agent-up-ubuntu-linux-x64.deb"), "");

        var commands = new RecordingCommandRunner((command, _) =>
        {
            if (IsUnixCliCommand(command, "agent-up", "start"))
                return new CommandResult(0, "Started workspace \"Installed Service Smoke Workspace\"", "");
            if (IsUnixCliCommand(command, "agent-up", "status"))
                return new CommandResult(0, "Name:       Installed Service Smoke Workspace\nState:      Running\n", "");
            if (command.FileName == "sudo" && command.Arguments.SequenceEqual(["apt-get", "purge", "-y", "agent-up"]))
            {
                RemoveUbuntuSystemFiles(systemRoot, "agent-up");
                return new CommandResult(0, "", "");
            }
            if (command.FileName == "sudo" && command.Arguments.SequenceEqual(["systemctl", "status", "agent-up-server.service", "--no-pager"]))
                return new CommandResult(1, "", "Unit agent-up-server.service could not be found.");
            if (command.FileName == "bash" && command.Arguments.Any(argument => argument.Contains("command -v agent-up", StringComparison.Ordinal)))
                return File.Exists(Path.Join(systemRoot, "usr", "share", "applications", "agent-up.desktop"))
                    ? new CommandResult(0, "/usr/bin/agent-up", "")
                    : new CommandResult(1, "", "agent-up: not found");
            return new CommandResult(0, "", "");
        });
        var probe = new FakeServerProbe("http://127.0.0.1:5000");
        var previousSkip = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL");

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", "1");
            var request = new InstalledServiceSmokeRequest("ubuntu", "linux-x64", artifactDir, workDir,
                SystemRoot: systemRoot);
            using var validator = new UbuntuInstalledServiceSmokeValidator(commands, probe, new NullRuntimeSecurityChecks(),
                new HttpClient(new FakeTraySessionHttpHandler()));
            var result = await validator.ValidateAsync(request);

            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Findings.Select(f => f.Message)));
            Assert.That(result.ServerUrl, Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(commands.Commands.Any(c => c.FileName == "bash" && c.Arguments.Any(a => a.Contains("command -v agent-up", StringComparison.Ordinal))), Is.True,
                "Agent-Up CLI probe must check for 'agent-up'");
            Assert.That(commands.Commands.Any(c => c.FileName == "sudo" && c.Arguments.SequenceEqual(["apt-get", "purge", "-y", "agent-up"])), Is.True,
                "Uninstall must purge 'agent-up' package");
            Assert.That(commands.Commands.Any(c => IsUnixCliCommand(c, "agent-up", "start")), Is.True,
                "CLI workspace smoke must invoke agent-up start");
            Assert.That(commands.Commands.Any(c => IsUnixCliCommand(c, "agent-up", "status")), Is.True,
                "CLI workspace smoke must invoke agent-up status");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_SKIP_REAL", previousSkip);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ProductConfigs_agentUpAndAcme_haveFullyDisjointSmokeTargets()
    {
        var agentUp = SmokeProductConfig.AgentUp;
        var acme = AcmeProduct;

        // Service names are distinct and neither is a substring of the other
        Assert.That(agentUp.ServiceName, Is.Not.EqualTo(acme.ServiceName));
        Assert.That(agentUp.ServiceName, Does.Not.Contain(acme.ServiceName));
        Assert.That(acme.ServiceName, Does.Not.Contain(agentUp.ServiceName));

        // CLI shim names are distinct and non-overlapping
        Assert.That(agentUp.CliShimName, Is.Not.EqualTo(acme.CliShimName));
        Assert.That(agentUp.CliShimName, Does.Not.Contain(acme.CliShimName));
        Assert.That(acme.CliShimName, Does.Not.Contain(agentUp.CliShimName));

        // Artifact base names are distinct
        Assert.That(agentUp.ArtifactBaseName, Is.Not.EqualTo(acme.ArtifactBaseName));

        // Install directory names are distinct
        Assert.That(agentUp.InstallDirName, Is.Not.EqualTo(acme.InstallDirName));

        // Ubuntu artifact paths built for each product are fully distinct
        var agentUpDeb = $"{agentUp.ArtifactBaseName}-ubuntu-linux-x64.deb";
        var acmeDeb = $"{acme.ArtifactBaseName}-ubuntu-linux-x64.deb";
        Assert.That(agentUpDeb, Is.Not.EqualTo(acmeDeb));

        // systemctl service arguments for each product cannot match the other's
        var agentUpServiceArg = $"{agentUp.ServiceName}.service";
        var acmeServiceArg = $"{acme.ServiceName}.service";
        Assert.That(agentUpServiceArg, Is.Not.EqualTo(acmeServiceArg));
        Assert.That(agentUpServiceArg, Does.Not.Contain(acme.ServiceName));
        Assert.That(acmeServiceArg, Does.Not.Contain(agentUp.ServiceName));
    }

    private static void SetupUbuntuSystemFiles(string systemRoot, string shimName)
    {
        var appsDir = Path.Join(systemRoot, "usr", "share", "applications");
        var pixmapsDir = Path.Join(systemRoot, "usr", "share", "pixmaps");
        var trayDir = Path.Join(systemRoot, "opt", shimName, "tray");
        var xdgDir = Path.Join(systemRoot, "etc", "xdg", "autostart");
        Directory.CreateDirectory(appsDir);
        Directory.CreateDirectory(pixmapsDir);
        Directory.CreateDirectory(trayDir);
        Directory.CreateDirectory(xdgDir);
        File.WriteAllText(Path.Join(appsDir, $"{shimName}.desktop"), "");
        File.WriteAllText(Path.Join(pixmapsDir, $"{shimName}.png"), "");
        File.WriteAllText(Path.Join(trayDir, "AgentUp.Tray"), "");
        File.WriteAllText(Path.Join(xdgDir, $"{shimName}-tray.desktop"), "");
    }

    private static void RemoveUbuntuSystemFiles(string systemRoot, string shimName)
    {
        File.Delete(Path.Join(systemRoot, "usr", "share", "applications", $"{shimName}.desktop"));
        File.Delete(Path.Join(systemRoot, "usr", "share", "pixmaps", $"{shimName}.png"));
        File.Delete(Path.Join(systemRoot, "opt", shimName, "tray", "AgentUp.Tray"));
        File.Delete(Path.Join(systemRoot, "etc", "xdg", "autostart", $"{shimName}-tray.desktop"));
    }

    private static bool IsUnixCliCommand(CommandSpec command, string shimName, string argument)
        => command.FileName == "bash"
           && command.Arguments.Any(a => a.Contains($"{shimName} {argument}", StringComparison.Ordinal))
           && command.Environment is not null
           && command.Environment.ContainsKey("AGENTUP_SMOKE_WORKING_DIRECTORY");

    private static string TempRoot(string tag)
        => Path.Join(Path.GetTempPath(), $"AgentUp-SmokeProduct-{tag}", $"{Guid.NewGuid():N}");
}
