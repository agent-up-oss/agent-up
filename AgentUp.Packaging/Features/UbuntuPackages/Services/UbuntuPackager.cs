using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Providers;

namespace AgentUp.Packaging.Features.UbuntuPackages.Services;

public sealed class UbuntuPackager
{
    private readonly ICommandRunner _commands;
    private readonly IPackageWriter _writer;
    private readonly PayloadStagingController _payloads;

    public UbuntuPackager(ICommandRunner commands, IPackageWriter writer, PayloadStagingController payloads)
    {
        _commands = commands;
        _writer = writer;
        _payloads = payloads;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = UbuntuPackageLayout.From(request);
        var manifest = UbuntuPackageManifest.From(request);
        await _payloads.StageAsync(new PayloadStagingRequest(
            request,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory),
            cancellationToken);

        new UbuntuPackageStager(_writer).Stage(request, layout, manifest);
        await _commands.RunAsync(new CommandSpec("dpkg-deb", ["--build", layout.DebRoot, layout.DebOutputPath]), cancellationToken);
    }
}
