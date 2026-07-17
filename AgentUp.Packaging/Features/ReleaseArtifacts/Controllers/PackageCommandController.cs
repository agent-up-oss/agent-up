using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;

public sealed class PackageCommandController
{
    private readonly IPackageCommandParser _parser;
    private readonly PackageCommandService _commands;

    public PackageCommandController(IPackageCommandParser parser, PackageCommandService commands)
    {
        _parser = parser;
        _commands = commands;
    }

    public async Task<int> ExecuteAsync(string[] args, TextWriter? standardError = null, CancellationToken cancellationToken = default)
    {
        standardError ??= Console.Error;
        var parsed = _parser.Parse(args);
        if (!parsed.Succeeded)
        {
            standardError.WriteLine(parsed.ErrorMessage);
            return 2;
        }

        var result = await _commands.ExecuteAsync(parsed.Command!, cancellationToken);
        if (result.ErrorMessage is not null)
            standardError.WriteLine(result.ErrorMessage);

        return result.ExitCode;
    }
}
