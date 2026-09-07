using System.Text.Json;
using AgentUp.CommitPolicy.Features.CommitPolicy.Models;

namespace AgentUp.CommitPolicy.Features.CommitPolicy.Providers;

public static class CommitsConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static CommitsConfiguration Load(string repoRoot)
    {
        var path = Path.Join(repoRoot, "agent-up.json");
        if (!File.Exists(path))
            return CommitsConfiguration.Empty;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("commits", out var commitsElement))
                return CommitsConfiguration.Empty;

            var raw = commitsElement.Deserialize<CommitsConfiguration>(Options);
            if (raw is null)
                return CommitsConfiguration.Empty;

            var projects = (raw.Projects ?? new Dictionary<string, CommitsProjectConfiguration>())
                .ToDictionary(
                    pair => pair.Key,
                    pair => new CommitsProjectConfiguration(pair.Value?.Test ?? [], pair.Value?.DependsOn ?? []),
                    StringComparer.Ordinal);

            return new CommitsConfiguration(raw.Build ?? [], raw.Test ?? [], projects);
        }
        catch (JsonException)
        {
            return CommitsConfiguration.Empty;
        }
        catch (IOException)
        {
            return CommitsConfiguration.Empty;
        }
    }
}
