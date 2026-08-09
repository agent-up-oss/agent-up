using System.Diagnostics;
using System.Text;
using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Composition;
using AgentUp.InstallerApp.Features.Logging.Tools;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using Avalonia;
using Avalonia.ReactiveUI;
using LocalInstaller.Sample;

var product = new ProductManifest(
    SampleProduct.Name,
    SampleProduct.Slug,
    SampleProduct.EnvironmentPrefix)
{
    Components =
    [
        ProductComponent.Cli,
        ProductComponent.Server,
        ProductComponent.Desktop,
        new ProductComponent("tray", "Tray", "Notification area app.")
    ],
    Manufacturer = SampleProduct.Name,
    WindowsUpgradeCode = SampleProduct.WindowsUpgradeCode
};

AppComposition.ConfigureProduct(
    product,
    SampleProduct.FakeInstallerVariable,
    SampleProduct.NixOsLookupOnlyVariable);

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    InstallerLog.WriteException("unhandled-exception", (Exception)e.ExceptionObject);

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    InstallerLog.WriteException("unobserved-task-exception", e.Exception);
    e.SetObserved();
};

InstallerLog.Write($"Installer starting: args=[{string.Join(", ", args)}]");
Console.Error.WriteLine($"[{SampleProduct.Name} Installer] Log: {InstallerLog.FilePath}");

try
{
    if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        return await RunWindowsUninstallAsync(product);

    SetBundledPayloadRoot(args, product);
    InstallerLog.Write($"Payload root: {Environment.GetEnvironmentVariable(product.PayloadRootVariable) ?? "(not set)"}");

    var commandLine = AppComposition.CreateCommandLineController();
    if (commandLine.ShouldRunCommandLine(args))
    {
        var adapter = InstallerPlatformAdapterFactory.Create(
            product,
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable(SampleProduct.FakeInstallerVariable),
            Environment.GetEnvironmentVariable(SampleProduct.NixOsLookupOnlyVariable) == "1" || InstallerPlatformAdapterFactory.IsNixOsHost());
        return await commandLine.RunAsync(adapter, product, args, Console.Out, Console.Error);
    }

    InstallerLog.Write("Starting GUI");
    return AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI()
        .StartWithClassicDesktopLifetime(args);
}
catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
{
    InstallerLog.WriteException("startup", exception);
    throw;
}

static void SetBundledPayloadRoot(string[] args, ProductManifest product)
{
    if (Environment.GetEnvironmentVariable(SampleProduct.NixOsLookupOnlyVariable) == "1" || InstallerPlatformAdapterFactory.IsNixOsHost())
        return;

    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(product.PayloadRootVariable)))
        return;

    var payloadRoot = PayloadRootFromArgs(args)
        ?? InstallerPlatformAdapterFactory.ResolvePayloadRoot(AppContext.BaseDirectory, product);

    Environment.SetEnvironmentVariable(product.PayloadRootVariable, payloadRoot);
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
            : Path.GetFullPath(Path.Join(AppContext.BaseDirectory, configured));
    }

    return null;
}

static async Task<int> RunWindowsUninstallAsync(ProductManifest product)
{
    if (!OperatingSystem.IsWindows())
        return 0;

    var scriptPath = WindowsInstallerPaths.ForProduct(product).UninstallScriptPath;
    if (!File.Exists(scriptPath))
        return 0;

    var command = $"& '{scriptPath.Replace("'", "''")}'";
    var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
        UseShellExecute = false,
        CreateNoWindow = true
    });

    if (process is null)
        return 1;

    await process.WaitForExitAsync();
    return process.ExitCode;
}
