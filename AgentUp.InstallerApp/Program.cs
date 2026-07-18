using Avalonia;
using Avalonia.ReactiveUI;
using AgentUp.InstallerApp;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using System.Diagnostics;
using System.Text;

if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
    return await RunWindowsUninstallAsync();

SetBundledPayloadRoot(args);

return AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);

static void SetBundledPayloadRoot(string[] args)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable)))
        return;

    var payloadRoot = PayloadRootFromArgs(args) ?? Path.Combine(AppContext.BaseDirectory, "payload");
    if (Directory.Exists(Path.Combine(payloadRoot, "desktop")) &&
        Directory.Exists(Path.Combine(payloadRoot, "server")) &&
        Directory.Exists(Path.Combine(payloadRoot, "cli")))
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, payloadRoot);
    }
}

static string? PayloadRootFromArgs(string[] args)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (!args[index].Equals("--payload-root", StringComparison.OrdinalIgnoreCase))
            continue;

        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            throw new InvalidOperationException("--payload-root requires a value.");

        var configured = args[index + 1];
        return Path.IsPathFullyQualified(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }

    return null;
}

static async Task<int> RunWindowsUninstallAsync()
{
    if (!OperatingSystem.IsWindows())
        return 0;

    var scriptPath = WindowsInstallerPaths.SystemDefault().UninstallScriptPath;
    if (!File.Exists(scriptPath))
        return 0;

    var command = $"& '{scriptPath.Replace("'", "''")}'";
    var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
        UseShellExecute = false
    });

    if (process is null)
        return 1;

    await process.WaitForExitAsync();
    return process.ExitCode;
}
