using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;

public sealed class WindowsInstalledServiceSmokeValidator : InstalledServiceSmokeValidator
{
    public WindowsInstalledServiceSmokeValidator(ICommandRunner commands, IServerProbe serverProbe, IRuntimeSecurityChecks securityChecks)
        : base(commands, serverProbe, securityChecks)
    {
    }

    protected override async Task<InstalledServiceContext?> InstallAsync(
        InstalledServiceSmokeRequest request,
        FileAssertions assert,
        CancellationToken cancellationToken)
    {
        var product = request.Product;
        var installer = Path.Join(request.ArtifactDirectory, $"{product.ArtifactBaseName}-windows-{request.RuntimeId}.exe");
        var productMsi = Path.Join(request.ArtifactDirectory, $"{product.ArtifactBaseName}-windows-{request.RuntimeId}.msi");
        var installDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), product.InstallDirName);
        assert.FileExists(installer, "installed.windows.artifact");
        assert.FileExists(productMsi, "installed.windows.product.msi");
        if (!File.Exists(installer) || !File.Exists(productMsi))
            return null;

        var installLog = Path.Join(request.WorkDirectory, "windows-msi-install.log");
        await RunMsiAsync(assert, ["/i", productMsi, "/qn", "/norestart", "/l*vx!", installLog], installLog, "installed.windows.install", cancellationToken);
        await RunRequiredAsync(assert, new CommandSpec("sc.exe", ["start", product.ServiceName]), "installed.windows.service.start", cancellationToken);

        var cli = Path.Join(installDir, "cli", "AgentUp.CLI.exe");
        assert.FileExists(Path.Join(installDir, "bin", $"{product.CliShimName}.cmd"), "installed.windows.path.shim");
        assert.FileExists(cli, "installed.windows.cli");

        var pathCheck = $"$installDir = [System.IO.Path]::GetFullPath($env:AGENTUP_INSTALL_DIR); $uninstallRoots = @('HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall', 'HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall'); $registration = $uninstallRoots | Where-Object {{ Test-Path $_ }} | ForEach-Object {{ Get-ChildItem $_ }} | ForEach-Object {{ Get-ItemProperty $_.PSPath }} | Where-Object {{ $_.DisplayName -eq '{product.DisplayName}' -or $_.DisplayName -eq '{product.DisplayName} Setup' }} | Select-Object -First 1; if (-not $registration) {{ throw '{product.DisplayName} uninstall registration missing' }}; $path = [Environment]::GetEnvironmentVariable('Path', 'Machine'); $bin = [System.IO.Path]::GetFullPath((Join-Path $installDir 'bin')).TrimEnd('\\'); $entries = ($path -split ';' | Where-Object {{ $_ }} | ForEach-Object {{ [System.IO.Path]::GetFullPath($_).TrimEnd('\\') }}); if (-not ($entries | Where-Object {{ [string]::Equals($_, $bin, [System.StringComparison]::OrdinalIgnoreCase) }})) {{ throw \"{product.DisplayName} PATH entry missing: $bin\" }}";

        await RunRequiredAsync(
            assert,
            new CommandSpec("powershell.exe", ["-NoProfile", "-Command", pathCheck], Environment: new Dictionary<string, string> { ["AGENTUP_INSTALL_DIR"] = installDir }),
            "installed.windows.registration",
            cancellationToken);

        return new InstalledServiceContext(
            "cmd.exe",
            InstalledCliEnvironment(Path.Join(installDir, "bin")),
            [new CommandSpec("msiexec.exe", ["/x", productMsi, "/qn", "/norestart", "/l*vx!", Path.Join(request.WorkDirectory, "windows-msi-uninstall.log")])],
            [new CommandSpec("powershell.exe", ["-NoProfile", "-Command", $"Get-Service {product.ServiceName} -ErrorAction SilentlyContinue | Format-List *"])]);
    }

    private static Dictionary<string, string> InstalledCliEnvironment(string binDirectory)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return new Dictionary<string, string>
        {
            ["PATH"] = string.IsNullOrWhiteSpace(path)
                ? binDirectory
                : binDirectory + Path.PathSeparator + path
        };
    }

    private async Task RunMsiAsync(FileAssertions assert, string[] arguments, string logPath, string code, CancellationToken cancellationToken)
    {
        var result = await RunAsync(new CommandSpec("msiexec.exe", arguments), cancellationToken);
        if (result.ExitCode == 0)
            return;

        var log = File.Exists(logPath) ? await File.ReadAllTextAsync(logPath, cancellationToken) : "";
        assert.Error(code, $"msiexec.exe failed with exit code {result.ExitCode}: {result.Stderr}{result.Stdout}{Environment.NewLine}{SummarizeMsiLog(log)}");
    }

    private static string SummarizeMsiLog(string log)
    {
        if (string.IsNullOrWhiteSpace(log))
            return "MSI log was empty or missing.";

        var lines = log.ReplaceLineEndings("\n").Split('\n');
        var returnValue3 = Array.FindIndex(lines, line => line.Contains("Return value 3", StringComparison.OrdinalIgnoreCase));
        if (returnValue3 >= 0)
            return Window(lines, Math.Max(0, returnValue3 - 40), Math.Min(lines.Length, returnValue3 + 41));

        var diagnosticLines = lines
            .Where(line =>
                line.Contains("Error ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("ActionStart", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Action ended", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Value 3", StringComparison.OrdinalIgnoreCase))
            .TakeLast(120)
            .ToArray();

        if (diagnosticLines.Length > 0)
            return string.Join(Environment.NewLine, diagnosticLines);

        return Window(lines, Math.Max(0, lines.Length - 160), lines.Length);
    }

    private static string Window(string[] lines, int start, int end)
        => string.Join(Environment.NewLine, lines[start..end]);
}
