using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Shared.Factories;
using LocalInstaller.Core.Shared.Models;

namespace LocalInstaller.Packaging.Composition;

public static class LocalInstallerPackager
{
    public static LocalInstallerPackagerBuilder Create(string[] args)
        => new(args);
}

public sealed class LocalInstallerPackagerBuilder
{
    private readonly string[] _args;
    private readonly List<PackageProductArtifact> _installerOptions = [];
    private PackageProductArtifact? _installerApplication;
    private string? _productName;
    private string? _slug;
    private string? _environmentPrefix;
    private string? _manufacturer;
    private string? _windowsUpgradeCode;
    private string? _windowsServiceName;
    private string? _windowsCliShimName;
    private string? _windowsServerUrl;

    internal LocalInstallerPackagerBuilder(string[] args)
        => _args = args;

    public LocalInstallerPackagerBuilder UseProductManifest<TManifest>()
        where TManifest : LocalInstallerProductManifest, new()
    {
        var manifest = new TManifest();
        _productName = manifest.ProductName;
        _slug = manifest.Slug;
        _environmentPrefix = manifest.EnvironmentPrefix;
        _manufacturer = manifest.Manufacturer;
        return this;
    }

    public LocalInstallerPackagerBuilder InstallerOptionCli<TManifest>()
        where TManifest : LocalInstallerCliManifest, new()
        => InstallerOption<TManifest>();

    public LocalInstallerPackagerBuilder InstallerOptionServer<TManifest>()
        where TManifest : LocalInstallerServerManifest, new()
    {
        var manifest = new TManifest();
        _windowsServiceName ??= manifest.ServiceName;
        _windowsServerUrl ??= manifest.ServerUrl;
        _installerOptions.Add(PackageProductArtifact.From(manifest.ToDescriptor()));
        return this;
    }

    public LocalInstallerPackagerBuilder InstallerOptionDesktop<TManifest>()
        where TManifest : LocalInstallerDesktopManifest, new()
        => InstallerOption<TManifest>();

    public LocalInstallerPackagerBuilder InstallerOptionTray<TManifest>()
        where TManifest : LocalInstallerTrayManifest, new()
        => InstallerOption<TManifest>();

    public LocalInstallerPackagerBuilder InstallerApplication<TManifest>()
        where TManifest : LocalInstallerInstallerAppManifest, new()
    {
        _installerApplication = PackageProductArtifact.From(new TManifest().ToDescriptor());
        return this;
    }

    public LocalInstallerPackagerBuilder Windows(Action<LocalInstallerPackagingWindowsOptions> configure)
    {
        var options = new LocalInstallerPackagingWindowsOptions();
        configure(options);
        _windowsUpgradeCode = options.UpgradeCode;
        _windowsServiceName = options.ServiceName ?? _windowsServiceName;
        _windowsCliShimName = options.CliShimName;
        _windowsServerUrl = options.ServerUrl ?? _windowsServerUrl;
        return this;
    }

    public Task<int> RunAsync()
    {
        var product = BuildProduct();
        return new PackagingServiceRegistry(product).PackageCommands.ExecuteAsync(_args);
    }

    private LocalInstallerPackagerBuilder InstallerOption<TManifest>()
        where TManifest : LocalInstallerArtifactManifest, new()
    {
        _installerOptions.Add(PackageProductArtifact.From(new TManifest().ToDescriptor()));
        return this;
    }

    private PackageProductManifest BuildProduct()
    {
        if (string.IsNullOrWhiteSpace(_productName))
            throw new InvalidOperationException("LocalInstaller.Packaging requires a product manifest.");
        if (string.IsNullOrWhiteSpace(_slug))
            throw new InvalidOperationException("LocalInstaller.Packaging requires a product slug.");
        if (string.IsNullOrWhiteSpace(_environmentPrefix))
            throw new InvalidOperationException("LocalInstaller.Packaging requires an environment prefix.");

        var duplicate = _installerOptions.GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Installer option '{duplicate.Key}' is registered more than once.");

        var cli = _installerOptions.FirstOrDefault(c => c.Target == LocalInstallerArtifactTarget.Cli);

        return new PackageProductManifest(_productName, _slug, _environmentPrefix)
        {
            Manufacturer = _manufacturer,
            WindowsUpgradeCode = _windowsUpgradeCode,
            WindowsServiceName = _windowsServiceName,
            WindowsCliShimName = _windowsCliShimName ?? cli?.ExecutableName,
            WindowsServerUrl = _windowsServerUrl,
            InstallerApplication = _installerApplication,
            InstallerOptions = _installerOptions.ToArray()
        };
    }
}

public sealed class LocalInstallerPackagingWindowsOptions
{
    public string? UpgradeCode { get; private set; }
    public string? ServiceName { get; private set; }
    public string? CliShimName { get; private set; }
    public string? ServerUrl { get; private set; }

    public LocalInstallerPackagingWindowsOptions WithUpgradeCode(string upgradeCode)
    {
        UpgradeCode = upgradeCode;
        return this;
    }

    public LocalInstallerPackagingWindowsOptions WithServiceName(string serviceName)
    {
        ServiceName = serviceName;
        return this;
    }

    public LocalInstallerPackagingWindowsOptions WithCliShimName(string cliShimName)
    {
        CliShimName = cliShimName;
        return this;
    }

    public LocalInstallerPackagingWindowsOptions WithServerUrl(string serverUrl)
    {
        ServerUrl = serverUrl;
        return this;
    }
}
