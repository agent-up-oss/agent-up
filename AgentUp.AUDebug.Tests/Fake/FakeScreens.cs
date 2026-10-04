using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeProductScreenSurface : IProductScreenSurface
{
    public FakeProductScreenSurface(string surface) => Surface = surface;

    public string Surface { get; }
    public bool Opened { get; private set; }
    public bool Disposed { get; private set; }
    public List<ScreenStepDto> Steps { get; } = [];
    public List<string> Captures { get; } = [];
    public Exception? CaptureException { get; set; }
    public bool DelayUntilCanceled { get; set; }

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        Opened = true;
        return Task.CompletedTask;
    }

    public async Task RunAsync(IReadOnlyList<ScreenStepDto> steps, CancellationToken cancellationToken)
    {
        Steps.AddRange(steps);
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public Task CaptureAsync(string outputPath, CancellationToken cancellationToken)
    {
        Captures.Add(outputPath);
        return CaptureException is null ? Task.CompletedTask : Task.FromException(CaptureException);
    }

    /// <summary>What this surface reports the screen is showing. Null stands for Desktop, which
    /// has no text channel.</summary>
    public string? Text { get; set; }

    public Task<string?> ReadTextAsync(CancellationToken cancellationToken) => Task.FromResult(Text);

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class FakeScreenSurfaceHost : IScreenSurfaceHost
{
    public List<string> Started { get; } = [];
    public bool Disposed { get; private set; }

    public Task StartAsync(string surface, CancellationToken cancellationToken)
    {
        Started.Add(surface);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class FakeScreenCaptureStore : IScreenCaptureStore
{
    /// <summary>Stands for a route whose steps changed nothing on the client.</summary>
    public bool UnchangedFromPrevious { get; set; }

    public bool IsUnchangedFrom(string path, string? previousPath)
        => UnchangedFromPrevious && previousPath is not null;

    public List<string> Reset0 { get; } = [];
    public ScreenRunManifestDto? Manifest { get; private set; }

    public string Reset(string surface)
    {
        Reset0.Add(surface);
        return $"artifacts/product-screens/{surface}";
    }

    public string CapturePath(string surface, string view) => $"artifacts/product-screens/{surface}/{view}.png";

    public string Relative(string path) => path;

    public string WriteManifest(ScreenRunManifestDto manifest)
    {
        Manifest = manifest;
        return "artifacts/product-screens/screens.json";
    }
}

public sealed class FakeProductScreenCatalog : IProductScreenCatalog
{
    private readonly Dictionary<string, IReadOnlyList<ProductScreenDto>> _screens = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Surfaces { get; set; } = [];

    public void Add(string surface, params ProductScreenDto[] screens)
    {
        _screens[surface] = screens;
        Surfaces = [.. Surfaces.Append(surface).Distinct(StringComparer.Ordinal)];
    }

    public IReadOnlyList<ProductScreenDto> Screens(string surface)
        => _screens.TryGetValue(surface, out var screens) ? screens : [];
}

public sealed class FakeScreenReadyProbe : IScreenReadyProbe
{
    public List<string> Urls { get; } = [];
    public Dictionary<string, bool> ReadyUrls { get; } = new(StringComparer.Ordinal);
    public bool DesktopWindowPresent { get; set; }
    public int DesktopWaits { get; private set; }
    public int UrlWaits { get; private set; }

    public Task<bool> IsReadyAsync(string url, CancellationToken cancellationToken)
    {
        Urls.Add(url);
        return Task.FromResult(ReadyUrls.GetValueOrDefault(url, false));
    }

    public Task WaitForUrlAsync(string url, CancellationToken cancellationToken)
    {
        UrlWaits++;
        return Task.CompletedTask;
    }

    public Task<bool> HasDesktopWindowAsync(CancellationToken cancellationToken)
        => Task.FromResult(DesktopWindowPresent);

    public Task WaitForDesktopWindowAsync(CancellationToken cancellationToken)
    {
        DesktopWaits++;
        return Task.CompletedTask;
    }
}

public sealed class FakeScreenRunStore : IScreenRunStore
{
    public ScreenRunManifestDto? Manifest { get; set; }

    public ScreenRunManifestDto? Read() => Manifest;
}

public sealed class FakeDocumentedScreens : IDocumentedScreens
{
    public List<ScreenshotSceneCopyDto> Scenes { get; } = [];

    public IReadOnlyList<ScreenshotSceneCopyDto> Read() => Scenes;
}
