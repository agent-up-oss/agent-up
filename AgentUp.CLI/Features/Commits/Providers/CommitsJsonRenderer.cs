using System.Text.Json;
using AgentUp.CLI.Features.Commits.Interfaces;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsJsonRenderer : ICommitsJsonRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);
}
