using System.Text.Json;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>Reads back the manifest the last screens run wrote.</summary>
public sealed class ScreenRunStore : IScreenRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IDebugPathValidator _paths;

    public ScreenRunStore(IDebugPathValidator paths) => _paths = paths;

    public ScreenRunManifestDto? Read()
    {
        var path = _paths.JoinUnderRoot(DebugLayout.ProductScreensDirectory, DebugLayout.ProductScreensManifestFile);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ScreenRunManifestDto>(File.ReadAllText(path), JsonOptions);
    }
}
