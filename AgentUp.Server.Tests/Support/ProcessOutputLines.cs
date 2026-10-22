using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.Server.Tests.Support;

internal static class ProcessOutputLines
{
    public static IReadOnlyList<string> Texts(this IEnumerable<ProcessOutputLine> lines)
        => [.. lines.Select(line => line.Text)];
}
