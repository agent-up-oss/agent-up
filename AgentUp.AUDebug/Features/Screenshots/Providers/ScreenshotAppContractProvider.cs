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
        var definition = ReadFakeServer();
        var allowed = CssClassSelector.Matches(css).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var htmlClasses = HtmlClasses(html);
        var unknown = htmlClasses.Where(name => !name.StartsWith("au-screenshot", StringComparison.Ordinal) && !allowed.Contains(name)).ToArray();
        if (unknown.Length > 0)
            throw new InvalidOperationException($"{scene.Id} uses classes that are not in the design-system catalog: {string.Join(", ", unknown)}.");

        var missingClass = scene.RequiredClasses.FirstOrDefault(required => !htmlClasses.Contains(required));
        if (missingClass is not null)
            throw new InvalidOperationException($"{scene.Id} HTML is missing required catalog class '{missingClass}'.");

        foreach (var copy in scene.Copy)
        {
            if (!html.Contains(copy, StringComparison.Ordinal))
                throw new InvalidOperationException($"{scene.Id} HTML is missing Demo copy '{copy}'.");
            if (!definition.Contains(copy, StringComparison.Ordinal))
                throw new InvalidOperationException($"{scene.Id} copy '{copy}' is not in AgentUp.FakeServer/definition.json.");
        }

        var sources = scene.AppSources.Select(ReadSource).ToArray();
        var joined = string.Join('\n', sources);
        var missingDesktop = scene.RequiredDesktopClasses.FirstOrDefault(desktopClass => !HasToken(joined, desktopClass));
        if (missingDesktop is not null)
            throw new InvalidOperationException($"{scene.Id} Desktop class '{missingDesktop}' is missing from {string.Join(", ", scene.AppSources)}.");

        var missingMobile = scene.RequiredMobileComponents.FirstOrDefault(component => !HasAuBox(joined, component));
        if (missingMobile is not null)
            throw new InvalidOperationException($"{scene.Id} Mobile component '{missingMobile}' is missing as auBox('{missingMobile}') from {string.Join(", ", scene.AppSources)}.");
    }

    private string ReadCss()
    {
        var path = _paths.JoinUnderRoot("AgentUp.DesignSystem", "dist", "web", "screenshots.css");
        if (!File.Exists(path))
            throw new InvalidOperationException("Design-system screenshot CSS is missing. Run au-debug build design-system.");
        return File.ReadAllText(path);
    }

    private string ReadFakeServer()
    {
        var path = _paths.JoinUnderRoot("AgentUp.FakeServer", "definition.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("AgentUp.FakeServer/definition.json is missing.");
        return File.ReadAllText(path);
    }

    private string ReadSource(string relative)
    {
        var path = _paths.JoinUnderRoot(relative.Split('/', StringSplitOptions.RemoveEmptyEntries));
        if (!File.Exists(path))
            throw new InvalidOperationException($"App source '{relative}' is missing for screenshot contract.");
        return File.ReadAllText(path);
    }

    private static HashSet<string> HtmlClasses(string html)
    {
        return HtmlClassAttribute.Matches(html)
            .SelectMany(match => match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool HasToken(string source, string token)
        => source.Contains(token, StringComparison.Ordinal);

    private static bool HasAuBox(string source, string component)
        => source.Contains($"auBox('{component}'", StringComparison.Ordinal)
           || source.Contains($"auBox(\"{component}\"", StringComparison.Ordinal);
}
