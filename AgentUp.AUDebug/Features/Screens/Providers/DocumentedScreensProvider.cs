using System.Text.Json;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// Reads the screens the design system documents out of its generated manifest.
/// </summary>
/// <remarks>
/// Only the id, the surface and the copy are read. The Screenshots slice owns the rest of a
/// scene; the comparison needs what a screen says, and reading that one field across the
/// boundary is cheaper than either slice knowing the other's shape.
/// </remarks>
public sealed class DocumentedScreensProvider : IDocumentedScreens
{
    private readonly IDebugPathValidator _paths;

    public DocumentedScreensProvider(IDebugPathValidator paths) => _paths = paths;

    public IReadOnlyList<ScreenshotSceneCopyDto> Read()
    {
        var path = _paths.JoinUnderRoot("AgentUp.DesignSystem", "dist", "web", "screenshots.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("Design-system screenshot manifest is missing. Run au-debug build design-system.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("scenes", out var scenes))
            throw new InvalidOperationException("Design-system screenshot manifest has no scenes.");

        var documented = scenes.EnumerateArray().Select(Read).ToArray();
        if (documented.Length == 0)
            throw new InvalidOperationException("Design-system screenshot manifest has no scenes.");
        return documented;
    }

    private static ScreenshotSceneCopyDto Read(JsonElement scene)
        => new(
            scene.GetProperty("id").GetString() ?? "",
            scene.TryGetProperty("surface", out var surface) ? surface.GetString() ?? "" : "",
            scene.TryGetProperty("copy", out var copy)
                ? copy.EnumerateArray().Select(entry => entry.GetString() ?? "").Where(entry => entry.Length > 0).ToArray()
                : []);
}
