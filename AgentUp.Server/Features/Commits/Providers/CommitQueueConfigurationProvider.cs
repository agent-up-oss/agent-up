using System.Text.Json;
using AgentUp.Server.Features.Commits.Interfaces;

namespace AgentUp.Server.Features.Commits.Providers;

public sealed class CommitQueueConfigurationProvider : ICommitQueueConfigurationProvider
{
    public bool IsGitQueueEnabled(string worktreePath)
    {
        var path = Path.Join(Path.GetFullPath(worktreePath), "agent-up.json");
        if (!File.Exists(path))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("commits", out var commits)
                && commits.ValueKind == JsonValueKind.Object
                && commits.TryGetProperty("enabled", out var enabled)
                && enabled.ValueKind is JsonValueKind.True;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The commits configuration in agent-up.json is not valid JSON.", exception);
        }
    }
}
