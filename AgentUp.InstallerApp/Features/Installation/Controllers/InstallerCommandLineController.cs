using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Features.Installation.Controllers;

public sealed class InstallerCommandLineController
{
    private readonly InstallerCommandLineService _service;

    public InstallerCommandLineController(InstallerCommandLineService service)
    {
        _service = service;
    }

    public bool ShouldRunCommandLine(string[] args)
        => _service.ShouldRunCommandLine(args);

    public async Task<int> RunAsync(
        IInstallerPlatformAdapter adapter,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
        => await _service.RunAsync(adapter, args, output, error, cancellationToken);

    public async Task<int> RunAsync(
        IInstallerPlatformAdapter adapter,
        ProductManifest manifest,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
        => await _service.RunAsync(adapter, manifest, args, output, error, cancellationToken);
}
