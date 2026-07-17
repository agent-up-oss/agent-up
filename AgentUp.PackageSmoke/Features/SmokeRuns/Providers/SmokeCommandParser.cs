using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Providers;

public sealed class SmokeCommandParser : ISmokeCommandParser
{
    public static readonly string Usage = "Usage: AgentUp.PackageSmoke <validate-package|validate-installed-service> <platform> <runtime-id> <artifact-dir> <work-dir>"
        + Environment.NewLine
        + "   or: AgentUp.PackageSmoke validate-installer-flow <platform> <work-dir> [payload-root]";

    public SmokeCommandParseResult Parse(string[] args)
    {
        if (args.Length == 3 && args[0] == "validate-installer-flow")
            return Success(InstallerFlow(args[1], args[2], payloadRoot: null));

        if (args.Length == 4 && args[0] == "validate-installer-flow")
            return Success(InstallerFlow(args[1], args[2], Path.GetFullPath(args[3])));

        if (args.Length == 5 && args[0] is "validate-package" or "validate-installed-service")
        {
            return Success(new SmokeCommandRequest(
                args[0],
                args[1],
                args[2],
                Path.GetFullPath(args[3]),
                Path.GetFullPath(args[4]),
                PayloadRoot: null));
        }

        return new SmokeCommandParseResult(null, Usage);
    }

    private static SmokeCommandParseResult Success(SmokeCommandRequest request)
        => new(request, Usage);

    private static SmokeCommandRequest InstallerFlow(string platform, string workDirectory, string? payloadRoot)
        => new(
            "validate-installer-flow",
            platform,
            RuntimeId: "",
            ArtifactDirectory: "",
            Path.GetFullPath(workDirectory),
            payloadRoot);
}
