using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Screenshots.Providers;

public sealed class ScreenshotMediaStore : IScreenshotMediaStore
{
    private readonly IDebugPathValidator _paths;

    public ScreenshotMediaStore(IDebugPathValidator paths) => _paths = paths;

    public string HtmlPath(ScreenshotSceneDto scene)
        => _paths.JoinUnderRoot("AgentUp.DesignSystem", "dist", "web", "screenshots", scene.HtmlFile);

    public string MediaPath(string mediaFile)
        => _paths.JoinUnderRoot("media", Path.GetFileName(mediaFile));

    public string HeroPath()
        => _paths.JoinUnderRoot("media", "screenshot.png");

    public string StagingPath(string fileName)
    {
        Directory.CreateDirectory(_paths.ScreenshotsDirectory);
        return _paths.EnsureUnderRoot(Path.Join(_paths.ScreenshotsDirectory, "staging", Path.GetFileName(fileName)));
    }

    public string CapturePath(string sceneId)
    {
        Directory.CreateDirectory(_paths.ScreenshotsDirectory);
        var name = $"{Sanitize(sceneId)}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.png";
        return _paths.EnsureUnderRoot(Path.Join(_paths.ScreenshotsDirectory, name));
    }

    public string FileUrl(string path)
        => new Uri(_paths.EnsureUnderRoot(path)).AbsoluteUri;

    public bool Exists(string path)
        => File.Exists(_paths.EnsureUnderRoot(path));

    public void Copy(string source, string destination)
    {
        var from = _paths.EnsureUnderRoot(source);
        var to = _paths.EnsureUnderRoot(destination);
        EnsureParent(to);
        File.Copy(from, to, overwrite: true);
    }

    public void EnsureParent(string path)
    {
        var full = _paths.EnsureUnderRoot(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    }

    private static string Sanitize(string sceneId)
    {
        if (sceneId.Length == 0)
            return "scene";
        var chars = sceneId.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray();
        return new string(chars);
    }
}
