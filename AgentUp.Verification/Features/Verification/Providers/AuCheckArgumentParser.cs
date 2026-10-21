using System.Globalization;
using AgentUp.Verification.Features.Verification.DTOs;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>Parses <c>au-check</c> arguments into a command invocation.</summary>
public sealed class AuCheckArgumentParser
{
    public AuCheckInvocation Parse(string[] args, string workingDirectory)
    {
        string? command = null;
        string? checkId = null;
        string? repository = null;
        string? config = null;
        string? report = null;
        double? minimum = null;
        var configExplicit = false;
        var runMissing = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg is "--help" or "-h")
                return Help(workingDirectory);

            if (arg is "--repo" or "--config" or "--report" or "--min")
            {
                if (index + 1 >= args.Length)
                    return Invalid(workingDirectory, $"Missing value for {arg}.");

                var value = args[++index];
                switch (arg)
                {
                    case "--repo":
                        repository = value;
                        break;
                    case "--config":
                        config = value;
                        configExplicit = true;
                        break;
                    case "--report":
                        report = value;
                        break;
                    default:
                        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                            return Invalid(workingDirectory, $"--min '{value}' is not a number.");
                        minimum = parsed;
                        break;
                }

                continue;
            }

            if (arg == "--run")
            {
                runMissing = true;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
                return Invalid(workingDirectory, $"Unknown option '{arg}'.");

            if (command is null)
            {
                command = arg;
                continue;
            }

            if (checkId is null)
            {
                checkId = arg;
                continue;
            }

            return Invalid(workingDirectory, $"Unexpected argument '{arg}'.");
        }

        if (command is null or "help")
            return Help(workingDirectory);

        if (command is not ("plan" or "run" or "guard" or "coverage" or "slices"))
            return Invalid(workingDirectory, $"Unknown command '{command}'.");

        if (checkId is not null && command != "run")
            return Invalid(workingDirectory, $"Command '{command}' does not take a check id.");

        var root = Path.GetFullPath(repository ?? workingDirectory);
        return new AuCheckInvocation(
            command,
            checkId,
            root,
            config ?? "agent-up.json",
            configExplicit,
            report,
            runMissing,
            minimum,
            null);
    }

    private static AuCheckInvocation Help(string workingDirectory)
        => new("help", null, workingDirectory, "agent-up.json", false, null, false, null, null);

    private static AuCheckInvocation Invalid(string workingDirectory, string error)
        => new("invalid", null, workingDirectory, "agent-up.json", false, null, false, null, error);
}
