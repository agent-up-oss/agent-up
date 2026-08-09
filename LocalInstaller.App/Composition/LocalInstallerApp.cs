using System.Diagnostics;
using System.Text;
using AgentUp.InstallerApp.Features.Logging.Tools;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using Avalonia;
using Avalonia.ReactiveUI;

namespace AgentUp.InstallerApp.Composition;

public static class LocalInstallerApp
{
    public static LocalInstallerAppBuilder Create(string[] args)
        => new(args);
}

public sealed class LocalInstallerAppBuilder
{
    private readonly string[] _args;
    private readonly List<ProductComponent> _components = [];
    private string? _productName;
    private string? _slug;
    private string? _environmentPrefix;
    private string? _manufacturer;
    private string? _upgradeCode;
    private string? _fakeInstallerVariable;
    private string? _nixOsLookupOnlyVariable;

    internal LocalInstallerAppBuilder(string[] args)
        => _args = args;

    public LocalInstallerAppBuilder Product(string name, string slug, string environmentPrefix)
    {
        _productName = name;
        _slug = slug;
        _environmentPrefix = environmentPrefix;
        return this;
    }

    public LocalInstallerAppBuilder Component(string id, string displayName, string description)
    {
        _components.Add(new ProductComponent(id, displayName, description));
        return this;
    }

    public LocalInstallerAppBuilder Manufacturer(string manufacturer)
    {
        _manufacturer = manufacturer;
        return this;
    }

    public LocalInstallerAppBuilder UpgradeCode(string upgradeCode)
    {
        _upgradeCode = upgradeCode;
        return this;
    }

    public LocalInstallerAppBuilder FakeInstallerVariable(string variable)
    {
        _fakeInstallerVariable = variable;
        return this;
    }

    public LocalInstallerAppBuilder NixOsLookupOnlyVariable(string variable)
    {
        _nixOsLookupOnlyVariable = variable;
        return this;
    }

    public async Task<int> RunAsync<TApplication>()
        where TApplication : Application, new()
    {
        var product = BuildProduct();
        AppComposition.ConfigureProduct(product, _fakeInstallerVariable, _nixOsLookupOnlyVariable);
        ConfigureExceptionLogging();

        InstallerLog.Write($"Installer starting: args=[{string.Join(", ", _args)}]");
        Console.Error.WriteLine($"[{product.ProductName} Installer] Log: {InstallerLog.FilePath}");

        try
        {
            if (_args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
                return await RunPlatformUninstallAsync(product);

            SetBundledPayloadRoot(product);
            InstallerLog.Write($"Payload root: {Environment.GetEnvironmentVariable(product.PayloadRootVariable) ?? "(not set)"}");

            var commandLine = AppComposition.CreateCommandLineController();
            if (commandLine.ShouldRunCommandLine(_args))
            {
                var adapter = InstallerPlatformAdapterFactory.Create(
                    product,
                    AppContext.BaseDirectory,
                    FakeInstaller(),
                    UseNixOsLookupOnlyMode());
                return await commandLine.RunAsync(adapter, product, _args, Console.Out, Console.Error);
            }

            InstallerLog.Write("Starting GUI");
            return AppBuilder.Configure<TApplication>()
                .UsePlatformDetect()
                .WithInterFont()
                .UseReactiveUI()
                .StartWithClassicDesktopLifetime(_args);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            InstallerLog.WriteException("startup", exception);
            throw;
        }
    }

    private ProductManifest BuildProduct()
    {
        if (string.IsNullOrWhiteSpace(_productName))
            throw new InvalidOperationException("LocalInstaller.App requires a product name.");
        if (string.IsNullOrWhiteSpace(_slug))
            throw new InvalidOperationException("LocalInstaller.App requires a product slug.");
        if (string.IsNullOrWhiteSpace(_environmentPrefix))
            throw new InvalidOperationException("LocalInstaller.App requires an environment prefix.");

        return new ProductManifest(_productName, _slug, _environmentPrefix)
        {
            Components = _components.ToArray(),
            Manufacturer = _manufacturer,
            WindowsUpgradeCode = _upgradeCode
        };
    }

    private void SetBundledPayloadRoot(ProductManifest product)
    {
        if (UseNixOsLookupOnlyMode())
            return;

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(product.PayloadRootVariable)))
            return;

        var payloadRoot = PayloadRootFromArgs()
            ?? InstallerPlatformAdapterFactory.ResolvePayloadRoot(AppContext.BaseDirectory, product);

        Environment.SetEnvironmentVariable(product.PayloadRootVariable, payloadRoot);
    }

    private string? PayloadRootFromArgs()
    {
        for (var index = 0; index < _args.Length; index++)
        {
            if (!_args[index].Equals("--payload-root", StringComparison.OrdinalIgnoreCase))
                continue;

            if (index + 1 >= _args.Length || string.IsNullOrWhiteSpace(_args[index + 1]))
                throw new InvalidOperationException("--payload-root requires a value.");

            var configured = _args[index + 1];
            return Path.IsPathFullyQualified(configured)
                ? configured
                : Path.GetFullPath(Path.Join(AppContext.BaseDirectory, configured));
        }

        return null;
    }

    private string? FakeInstaller()
        => _fakeInstallerVariable is null
            ? null
            : Environment.GetEnvironmentVariable(_fakeInstallerVariable);

    private bool UseNixOsLookupOnlyMode()
        => _nixOsLookupOnlyVariable is not null
           && Environment.GetEnvironmentVariable(_nixOsLookupOnlyVariable) == "1"
           || InstallerPlatformAdapterFactory.IsNixOsHost();

    private static void ConfigureExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            InstallerLog.WriteException("unhandled-exception", (Exception)e.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            InstallerLog.WriteException("unobserved-task-exception", e.Exception);
            e.SetObserved();
        };
    }

    private static async Task<int> RunPlatformUninstallAsync(ProductManifest product)
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
}
