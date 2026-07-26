using AgentUp.CLI.Features.Commits.DTOs;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsArgParser
{
    public (EnqueueRequest? Request, string? Error) Parse(string[] args)
    {
        string? slice = null;
        string? message = null;
        var files = new List<string>();
        var tests = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--slice" when i + 1 < args.Length:
                    slice = args[++i];
                    break;
                case "--message" when i + 1 < args.Length:
                    message = args[++i];
                    break;
                case "--files":
                    i++;
                    while (i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal))
                        files.Add(args[i++]);
                    i--;
                    break;
                case "--tests":
                    i++;
                    while (i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal))
                        tests.Add(args[i++]);
                    i--;
                    break;
                default:
                    return (null, $"Unknown argument: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(slice))
            return (null, "Missing required argument: --slice");
        if (string.IsNullOrWhiteSpace(message))
            return (null, "Missing required argument: --message");
        if (files.Count == 0)
            return (null, "Missing required argument: --files (at least one file required)");

        return (new EnqueueRequest(slice, message, files, tests), null);
    }
}
