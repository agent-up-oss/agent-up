using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Features.Installation.Controllers;

public sealed class InstallerCommandLineController
{
    public async Task<int> RunAsync(
        IInstallerPlatformAdapter adapter,
        ProductManifest manifest,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
        => await InstallerCommandLine.RunAsync(adapter, manifest, args, output, error, cancellationToken);
}
