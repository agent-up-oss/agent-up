using System.Net.WebSockets;
using System.Text.Json;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Features.Screenshots.Services;

public sealed class ScreenshotCommandService
{
    private readonly IScreenshotManifestStore _manifests;
    private readonly IScreenshotMediaStore _media;
    private readonly IWebScreenshotDriver _capture;
    private readonly IScreenshotAppContract _contract;
    private readonly IScreenshotPngComparer _comparer;
    private readonly IScreenshotLiveAppProbe _live;
    private readonly IHostSessionStore _sessions;
    private readonly IHostReadyProbe _probe;

    public ScreenshotCommandService(
        IScreenshotManifestStore manifests,
        IScreenshotMediaStore media,
        IWebScreenshotDriver capture,
        IScreenshotAppContract contract,
        IScreenshotPngComparer comparer,
        IScreenshotLiveAppProbe live,
        IHostSessionStore sessions,
        IHostReadyProbe probe)
    {
        _manifests = manifests;
        _media = media;
        _capture = capture;
        _contract = contract;
        _comparer = comparer;
        _live = live;
        _sessions = sessions;
        _probe = probe;
    }

    public Task<CommandResultDto> PersistAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, PersistCoreAsync);

    public Task<CommandResultDto> ValidateAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, ValidateCoreAsync);

    public Task<CommandResultDto> CaptureAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => RunAsync(command, cancellationToken, CaptureCoreAsync);

    private async Task<CommandResultDto> RunAsync(
        DebugCommandDto command,
        CancellationToken cancellationToken,
        Func<DebugCommandDto, CancellationToken, Task<CommandResultDto>> action)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            return await action(command, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s running screenshots {command.Action}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                        or JsonException
                                        or IOException
                                        or HttpRequestException
                                        or WebSocketException
                                        or UriFormatException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }

    private async Task<CommandResultDto> PersistCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var scenes = Selected(command);
        string? last = null;
        string? hero = null;
        foreach (var scene in scenes)
        {
            last = await CaptureSceneAsync(scene, _media.MediaPath(scene.MediaFile), cancellationToken);
            if (scene.Hero)
                hero = last;
        }

        if (hero is not null)
        {
            _media.Copy(hero, _media.HeroPath());
            last = _media.HeroPath();
        }

        return CommandResultDto.Ok($"Wrote {scenes.Count} design-system screenshot(s) to media/.", last);
    }

    private async Task<CommandResultDto> ValidateCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var scenes = Selected(command);
        foreach (var scene in scenes)
            _contract.Verify(scene);

        var mismatches = new List<string>();
        foreach (var scene in scenes)
        {
            var staged = _media.StagingPath(scene.MediaFile);
            await CaptureSceneAsync(scene, staged, cancellationToken);
            var comparison = _comparer.Compare(_media.MediaPath(scene.MediaFile), staged);
            if (!comparison.Match)
                mismatches.Add($"{scene.Id}: {comparison.Detail}");
            if (scene.Hero)
            {
                var hero = _comparer.Compare(_media.HeroPath(), staged);
                if (!hero.Match)
                    mismatches.Add($"media/screenshot.png: {hero.Detail}");
            }
        }

        if (command.Live)
            mismatches.AddRange(await LiveMismatchesAsync(scenes, cancellationToken));

        if (mismatches.Count > 0)
            return CommandResultDto.Fail("Screenshot validation failed:\n" + string.Join('\n', mismatches));

        var live = command.Live ? " Source, pixels, and live apps match." : " Source classes and regenerated pixels match.";
        return CommandResultDto.Ok($"Validated {scenes.Count} design-system screenshot(s).{live}");
    }

    private async Task<CommandResultDto> CaptureCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var scenes = Selected(command);
        string? last = null;
        foreach (var scene in scenes)
            last = await CaptureSceneAsync(scene, _media.CapturePath(scene.Id), cancellationToken);
        return CommandResultDto.Ok($"Wrote {scenes.Count} design-system screenshot(s).", last);
    }

    private async Task<string> CaptureSceneAsync(
        ScreenshotSceneDto scene,
        string destination,
        CancellationToken cancellationToken)
    {
        var html = _media.HtmlPath(scene);
        if (!_media.Exists(html))
            throw new InvalidOperationException($"Screenshot HTML '{scene.HtmlFile}' is missing. Run au-debug build design-system.");
        _media.EnsureParent(destination);
        await _capture.CaptureAsync(_media.FileUrl(html), destination, cancellationToken, null, scene.Width, scene.Height);
        return destination;
    }

    private IReadOnlyList<ScreenshotSceneDto> Selected(DebugCommandDto command)
    {
        var scenes = _manifests.Read().Scenes;
        if (scenes.Count == 0)
            throw new InvalidOperationException("Screenshot manifest has no scenes.");
        var surface = command.Action is "desktop" or "mobile" ? command.Action : null;
        IEnumerable<ScreenshotSceneDto> filtered = scenes;
        if (surface is not null)
            filtered = filtered.Where(scene => scene.Surface == surface);
        if (!string.IsNullOrWhiteSpace(command.View))
            filtered = filtered.Where(scene => scene.View == command.View || scene.Id == command.View);
        var selected = filtered.ToArray();
        if (selected.Length == 0)
            throw new InvalidOperationException(UnknownView(command, scenes));
        return selected;
    }

    private async Task<IReadOnlyList<string>> LiveMismatchesAsync(
        IReadOnlyList<ScreenshotSceneDto> scenes,
        CancellationToken cancellationToken)
    {
        if (_sessions.Read() is null && !await _probe.CheckAsync(DebugLayout.MobileUrl, cancellationToken))
            throw new InvalidOperationException("au-debug is not running. Start it with au-debug up before --live.");

        var mismatches = new List<string>();
        foreach (var scene in scenes.Where(scene => scene.Surface == "mobile" && scene.LivePath.Length > 0))
        {
            var html = await _live.ReadPageTextAsync($"{DebugLayout.MobileUrl}{scene.LivePath}", cancellationToken);
            mismatches.AddRange(scene.Copy
                .Where(copy => !html.Contains(copy, StringComparison.Ordinal))
                .Select(copy => $"{scene.Id} live page is missing Demo copy '{copy}'."));
        }

        return mismatches;
    }

    private static string UnknownView(DebugCommandDto command, IReadOnlyList<ScreenshotSceneDto> scenes)
    {
        var known = scenes
            .Where(scene => command.Action is not ("desktop" or "mobile") || scene.Surface == command.Action)
            .Select(scene => scene.View)
            .Distinct(StringComparer.Ordinal);
        return $"Error: unknown screenshot view '{command.View}'. Choose one of: {string.Join(", ", known)}.";
    }
}
