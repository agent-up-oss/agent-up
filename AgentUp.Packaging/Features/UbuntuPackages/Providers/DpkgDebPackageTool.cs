using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Features.UbuntuPackages.Providers;

public sealed class DpkgDebPackageTool : IUbuntuPackageTool
{
    private readonly ICommandRunner _commands;

    public DpkgDebPackageTool(ICommandRunner commands)
    {
        _commands = commands;
    }

    public Task BuildDebAsync(UbuntuPackageLayout layout, CancellationToken cancellationToken = default)
        => _commands.RunAsync(new CommandSpec("dpkg-deb", ["--build", layout.DebRoot, layout.DebOutputPath]), cancellationToken);
}
