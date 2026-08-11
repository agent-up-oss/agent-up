using LocalInstaller.Core.Shared.Models;
using LocalInstaller.Smoke.Features.SmokeRuns.DTOs;
using LocalInstaller.Smoke.Shared.Factories;

namespace LocalInstaller.Smoke.Composition;

public static class LocalInstallerSmoke
{
    public static LocalInstallerSmokeBuilder Create(string[] args)
        => new(args);
}

public sealed class LocalInstallerSmokeBuilder
{
    private readonly string[] _args;
    private readonly List<LocalInstallerArtifactDescriptor> _installerOptions = [];
    private string? _productName;
    private string? _slug;
    private string? _environmentPrefix;
    private string? _workspaceConfigFileName;

    internal LocalInstallerSmokeBuilder(string[] args)
        => _args = args;

    public LocalInstallerSmokeBuilder UseProductManifest<TManifest>()
        where TManifest : LocalInstallerProductManifest, new()
    {
        var manifest = new TManifest();
        _productName = manifest.ProductName;
        _slug = manifest.Slug;
        _environmentPrefix = manifest.EnvironmentPrefix;
        return this;
    }

    public LocalInstallerSmokeBuilder WorkspaceConfigFileName(string workspaceConfigFileName)
    {
        _workspaceConfigFileName = workspaceConfigFileName;
        return this;
    }

    public LocalInstallerSmokeBuilder InstallerApplication<TManifest>()
        where TManifest : LocalInstallerInstallerAppManifest, new()
        => InstallerOption<TManifest>();

    public LocalInstallerSmokeBuilder InstallerOptionCli<TManifest>()
        where TManifest : LocalInstallerCliManifest, new()
        => InstallerOption<TManifest>();

    public LocalInstallerSmokeBuilder InstallerOptionServer<TManifest>()
        where TManifest : LocalInstallerServerManifest, new()
        => InstallerOption<TManifest>();

    public LocalInstallerSmokeBuilder InstallerOptionDesktop<TManifest>()
        where TManifest : LocalInstallerDesktopManifest, new()
        => InstallerOption<TManifest>();

    public LocalInstallerSmokeBuilder InstallerOptionTray<TManifest>()
        where TManifest : LocalInstallerTrayManifest, new()
        => InstallerOption<TManifest>();

    public Task<int> RunAsync(TextWriter output, TextWriter error)
    {
        var controller = PackageSmokeServiceRegistry.CreateSmokeCommandController(BuildProduct());
        return controller.ExecuteAsync(_args, output, error);
    }

    private LocalInstallerSmokeBuilder InstallerOption<TManifest>()
        where TManifest : LocalInstallerArtifactManifest, new()
    {
        _installerOptions.Add(new TManifest().ToDescriptor());
        return this;
    }

    private SmokeProductManifest BuildProduct()
    {
        if (string.IsNullOrWhiteSpace(_productName) || string.IsNullOrWhiteSpace(_slug) || string.IsNullOrWhiteSpace(_environmentPrefix))
            throw new InvalidOperationException("LocalInstaller.Smoke requires a product manifest.");

        var installer = Required(LocalInstallerArtifactTarget.InstallerApp);
        var desktop = Required(LocalInstallerArtifactTarget.Desktop);
        var server = Required(LocalInstallerArtifactTarget.Server);
        var cli = Required(LocalInstallerArtifactTarget.Cli);
        var tray = Required(LocalInstallerArtifactTarget.Tray);

        return new SmokeProductManifest(
            ServiceName: _slug + "-server",
            CliShimName: _slug,
            ArtifactBaseName: _slug,
            DisplayName: _productName,
            InstallDirName: _productName,
            WorkspaceConfigFileName: _workspaceConfigFileName ?? _slug + ".json",
            InstallerExecutableName: installer.ExecutableName!,
            DesktopExecutableName: desktop.ExecutableName!,
            ServerExecutableName: server.ExecutableName!,
            CliExecutableName: cli.ExecutableName!,
            TrayExecutableName: tray.ExecutableName!);
    }

    private LocalInstallerArtifactDescriptor Required(LocalInstallerArtifactTarget target)
        => _installerOptions.FirstOrDefault(component => component.Target == target)
           ?? throw new InvalidOperationException($"LocalInstaller.Smoke requires a {target} manifest.");
}
