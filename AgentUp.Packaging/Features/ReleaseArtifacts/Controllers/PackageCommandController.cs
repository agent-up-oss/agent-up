using AgentUp.Packaging.Features.ReleaseArtifacts.Services;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;

public sealed class PackageCommandController
{
    private readonly PackageCommandService _commands;

    public PackageCommandController(PackageCommandService commands)
    {
        _commands = commands;
    }

    public Task<int> ExecuteAsync(string[] args, TextWriter? standardError = null, CancellationToken cancellationToken = default)
        => _commands.ExecuteAsync(args, standardError ?? Console.Error, cancellationToken);
}
