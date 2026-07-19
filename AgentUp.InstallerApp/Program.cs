using Avalonia;
using Avalonia.ReactiveUI;
using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using System.Diagnostics;
using System.Text;

try
{
    if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        return await RunWindowsUninstallAsync();

    SetBundledPayloadRoot(args);
    if (InstallerCommandLine.ShouldRunCommandLine(args))
        return await InstallerCommandLine.RunAsync(args, Console.Out, Console.Error);

    return AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI()
        .StartWithClassicDesktopLifetime(args);
}
catch (Exception exception)
{
    WriteStartupCrash(exception);
    throw;
}

static void SetBundledPayloadRoot(string[] args)
{
    if (InstallerPlatformAdapterFactory.UseNixOsLookupOnlyMode())
        return;

    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable)))
        return;

    var payloadRoot = PayloadRootFromArgs(args)
        ?? InstallerPlatformAdapterFactory.ResolvePayloadRoot(AppContext.BaseDirectory);

    Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, payloadRoot);
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

static void WriteStartupCrash(Exception exception)
{
    try
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "Logs",
            "Agent-Up");
        Directory.CreateDirectory(logDirectory);
        File.AppendAllText(
            Path.Combine(logDirectory, "installer-crash.log"),
            $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}");
    }
    catch
    {
        // Startup diagnostics must never replace the original failure.
    }
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
