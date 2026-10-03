using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeScreenshotManifestStore : IScreenshotManifestStore
{
    public List<ScreenshotSceneDto> Scenes { get; } = [];
    public Exception? ReadException { get; set; }

    public ScreenshotManifestDto Read()
    {
        if (ReadException is not null)
            throw ReadException;
        return new ScreenshotManifestDto { Scenes = Scenes };
    }
}

public sealed class FakeScreenshotMediaStore : IScreenshotMediaStore
{
    public string Root { get; set; } = "/tmp/screenshots";
    public HashSet<string> Existing { get; } = new(StringComparer.Ordinal);
    public List<(string Source, string Destination)> Copies { get; } = [];

    public string HtmlPath(ScreenshotSceneDto scene) => $"{Root}/html/{scene.HtmlFile}";
    public string MediaPath(string mediaFile) => $"{Root}/media/{mediaFile}";
    public string HeroPath() => $"{Root}/media/screenshot.png";
    public string StagingPath(string fileName) => $"{Root}/staging/{fileName}";
    public string CapturePath(string sceneId) => $"{Root}/capture/{sceneId}.png";
    public string FileUrl(string path) => $"file://{path}";
    public bool Exists(string path) => Existing.Contains(path);
    public void Copy(string source, string destination)
    {
        Copies.Add((source, destination));
        Existing.Add(destination);
    }

    public void EnsureParent(string path) => Existing.Add(path);
}

public sealed class FakeScreenshotAppContract : IScreenshotAppContract
{
    public List<string> Verified { get; } = [];
    public Exception? VerifyException { get; set; }

    public void Verify(ScreenshotSceneDto scene)
    {
        if (VerifyException is not null)
            throw VerifyException;
        Verified.Add(scene.Id);
    }
}

public sealed class FakeScreenshotPngComparer : IScreenshotPngComparer
{
    public bool Match { get; set; } = true;
    public string Detail { get; set; } = "identical pixels";
    public List<(string Expected, string Actual)> Compared { get; } = [];

    public ScreenshotCompareDto Compare(string expectedPath, string actualPath)
    {
        Compared.Add((expectedPath, actualPath));
        return new ScreenshotCompareDto(Match, Match ? 0 : 3, Detail);
    }
}

public sealed class FakeScreenshotLiveAppProbe : IScreenshotLiveAppProbe
{
    public Dictionary<string, string> Pages { get; } = new(StringComparer.Ordinal);
    public Exception? ReadException { get; set; }

    public Task<string> ReadPageTextAsync(string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReadException is not null)
            throw ReadException;
        return Task.FromResult(Pages.TryGetValue(url, out var html) ? html : "");
    }
}
