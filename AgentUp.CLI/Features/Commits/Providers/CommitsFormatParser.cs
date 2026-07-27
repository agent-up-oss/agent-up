using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsFormatParser : ICommitsFormatParser
{
    public (CommitsOutputFormat Format, string? Error) Parse(string[] args)
    {
        var format = CommitsOutputFormat.Text;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--format" when i + 1 < args.Length:
                    var value = args[++i];
                    if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        format = CommitsOutputFormat.Json;
                        break;
                    }

                    if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        format = CommitsOutputFormat.Text;
                        break;
                    }

                    return (format, $"Unsupported format: {value}");
                case "--format":
                    return (format, "Missing required value for --format");
                default:
                    return (format, $"Unknown argument: {args[i]}");
            }
        }

        return (format, null);
    }
}
