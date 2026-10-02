using System.Text.Json;
using System.Text.RegularExpressions;
using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Screenshots.Providers;

public sealed class ScreenshotAppContractProvider : IScreenshotAppContract
{
    private static readonly Regex HtmlClassAttribute = new(
        "class=\"([^\"]+)\"",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CssClassSelector = new(
        @"\.((?:au-[a-z0-9-]+))",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LayoutShell = new(
        @"^<([a-z][a-z0-9]*)\b[^>]*>\s*</\1>$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IDebugPathValidator _paths;
    private readonly IScreenshotMediaStore _media;

    public ScreenshotAppContractProvider(IDebugPathValidator paths, IScreenshotMediaStore media)
    {
        _paths = paths;
        _media = media;
    }

    public void Verify(ScreenshotSceneDto scene)
    {
        var htmlPath = _media.HtmlPath(scene);
        if (!_media.Exists(htmlPath))
            throw new InvalidOperationException($"Screenshot HTML '{scene.HtmlFile}' is missing. Run au-debug build design-system.");

        var html = File.ReadAllText(htmlPath);
        var css = ReadCss();
        var catalog = ReadCatalog();
        var allowed = CssClassSelector.Matches(css).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var htmlClasses = HtmlClasses(html);
        var unknown = htmlClasses.Where(name => !allowed.Contains(name)).ToArray();
        if (unknown.Length > 0)
            throw new InvalidOperationException($"{scene.Id} uses classes that are not in the design-system catalog: {string.Join(", ", unknown)}.");

        if (scene.Components.Count == 0)
            throw new InvalidOperationException($"{scene.Id} does not name catalog components to compose.");

        foreach (var id in scene.Components)
        {
            if (!catalog.TryGetValue(id, out var componentHtml))
                throw new InvalidOperationException($"{scene.Id} references unknown catalog component '{id}'.");
            if (IsLayoutShell(componentHtml))
            {
                var missing = ShellClasses(componentHtml).Where(name => !htmlClasses.Contains(name)).ToArray();
                if (missing.Length > 0)
                    throw new InvalidOperationException($"{scene.Id} HTML is missing catalog component '{id}'.");
                continue;
            }

            if (!html.Contains(componentHtml, StringComparison.Ordinal))
                throw new InvalidOperationException($"{scene.Id} HTML is missing catalog component '{id}'.");
        }
    }

    private string ReadCss()
    {
        var path = _paths.JoinUnderRoot("AgentUp.DesignSystem", "dist", "web", "screenshots.css");
        if (!File.Exists(path))
            throw new InvalidOperationException("Design-system screenshot CSS is missing. Run au-debug build design-system.");
        return File.ReadAllText(path);
    }

    private Dictionary<string, string> ReadCatalog()
    {
        var path = _paths.JoinUnderRoot("AgentUp.DesignSystem", "dist", "web", "catalog.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("Design-system catalog is missing. Run au-debug build design-system.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("surfaces", out var surfaces))
            throw new InvalidOperationException("Design-system catalog is missing surfaces.");

        var map = surfaces.EnumerateArray()
            .SelectMany(CatalogComponents)
            .Select(component => (
                Id: component.GetProperty("id").GetString(),
                Html: component.GetProperty("html").GetString()))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id) && entry.Html is not null)
            .ToDictionary(entry => entry.Id!, entry => entry.Html!, StringComparer.Ordinal);

        if (map.Count == 0)
            throw new InvalidOperationException("Design-system catalog has no components.");
        return map;
    }

    private static IEnumerable<JsonElement> CatalogComponents(JsonElement surface)
        => surface.TryGetProperty("components", out var components)
            ? components.EnumerateArray()
            : [];

    private static HashSet<string> HtmlClasses(string html)
    {
        return HtmlClassAttribute.Matches(html)
            .SelectMany(match => match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsLayoutShell(string html)
        => LayoutShell.IsMatch(html.Trim());

    private static IEnumerable<string> ShellClasses(string html)
    {
        var match = HtmlClassAttribute.Match(html);
        if (!match.Success) return [];
        return match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
