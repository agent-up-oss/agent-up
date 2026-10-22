using System.Text.Json;
using AgentUp.Desktop.Features.Console.Providers;

namespace AgentUp.Tests.Support;

/// <summary>
/// Formats application console snapshots the Runtime capability E2E fixture attaches to
/// start heartbeats. The Server returns <c>{ text, stream }</c> objects; Desktop already
/// parses that shape, so this helper reuses that parser instead of assuming a string array.
/// </summary>
internal static class WorkspaceConsoleOutput
{
    public static string FormatBlock(string application, JsonElement payload)
    {
        var lines = ConsoleApiClient.Parse(payload);
        if (lines.Count == 0)
            return "";

        return $"{application} console:{Environment.NewLine}"
            + string.Join(Environment.NewLine, lines.TakeLast(40));
    }
}
