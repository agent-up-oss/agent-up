using System.Text.Json;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>Where a screens run writes its PNGs and the manifest that accounts for them.</summary>
public sealed class ScreenCaptureStore : IScreenCaptureStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IDebugPathValidator _paths;

    public ScreenCaptureStore(IDebugPathValidator paths) => _paths = paths;

    public string Reset(string surface)
    {
        var directory = SurfaceDirectory(surface);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public string CapturePath(string surface, string view)
        => Path.Join(SurfaceDirectory(surface), $"{view}.png");

    public string Relative(string path)
        => Path.GetRelativePath(_paths.RepositoryRoot, path);

    public string WriteManifest(ScreenRunManifestDto manifest)
    {
        var path = _paths.JoinUnderRoot(DebugLayout.ProductScreensDirectory, DebugLayout.ProductScreensManifestFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        return path;
    }

    public bool IsUnchangedFrom(string path, string? previousPath)
    {
        if (previousPath is null || !File.Exists(path) || !File.Exists(previousPath)) return false;
        if (new FileInfo(path).Length != new FileInfo(previousPath).Length) return false;
        return File.ReadAllBytes(path).AsSpan().SequenceEqual(File.ReadAllBytes(previousPath));
    }

    private string SurfaceDirectory(string surface)
        => _paths.JoinUnderRoot(DebugLayout.ProductScreensDirectory, surface);
}
