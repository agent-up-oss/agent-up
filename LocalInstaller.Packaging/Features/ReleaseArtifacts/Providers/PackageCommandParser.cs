using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

public sealed class PackageCommandParser : IPackageCommandParser
{
    public const string Usage = "Usage: AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] [--payload-root <path>]";

    private readonly IEnvironmentVariableProvider _environment;

    public PackageCommandParser(IEnvironmentVariableProvider environment)
    {
        _environment = environment;
    }

    public PackageCommandParseResult Parse(string[] args)
    {
        if (args.Length is < 4 or > 7 || args[0] != "package")
            return PackageCommandParseResult.Failure(Usage);

        var outputDirectory = "artifacts";
        var payloadRoot = _environment.Get("AGENTUP_PACKAGE_PAYLOAD_ROOT");
        var index = 4;

        if (index < args.Length && args[index] != "--payload-root")
        {
            outputDirectory = args[index];
            index++;
        }

        if (index < args.Length)
        {
            if (index + 1 >= args.Length || args[index] != "--payload-root")
                return PackageCommandParseResult.Failure(Usage);

            payloadRoot = args[index + 1];
            index += 2;
        }

        if (index != args.Length)
            return PackageCommandParseResult.Failure(Usage);

        return PackageCommandParseResult.Success(new PackageCommand(args[1], args[2], args[3], outputDirectory, payloadRoot));
    }
}
