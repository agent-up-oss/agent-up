using System.Text.Json;
using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Screenshots.Providers;

public sealed class ScreenshotManifestStore : IScreenshotManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IDebugPathValidator _paths;

    public ScreenshotManifestStore(IDebugPathValidator paths) => _paths = paths;

    public ScreenshotManifestDto Read()
    {
        var path = _paths.JoinUnderRoot("AgentUp.DesignSystem", "dist", "web", "screenshots.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("Design-system screenshot manifest is missing. Run au-debug build design-system.");

        var json = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize<ScreenshotManifestDto>(json, JsonOptions)
                       ?? throw new JsonException("Screenshot manifest is empty.");
        if (manifest.Scenes.Count == 0)
            throw new InvalidOperationException("Screenshot manifest has no scenes.");
        return manifest;
    }
}
