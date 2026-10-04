using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Features.Screens.Models;

namespace AgentUp.AUDebug.Features.Screens.Services;

/// <summary>
/// Captures every page-assembly screen from the real clients, in a used state, against Demo.
/// </summary>
/// <remarks>
/// A run is one session per surface. The routes are ordered and build on each other, so the
/// screens are driven in catalog order and the session stays open across them; the capture
/// after each route is what that screen looked like once it had been used.
/// </remarks>
public sealed class ScreensCommandService
{
    private readonly IProductScreenCatalog _catalog;
    private readonly IScreenCaptureStore _store;
    private readonly IScreenSurfaceHost _host;
    private readonly Func<string, IProductScreenSurface> _surfaces;
    private readonly IScreenRunStore _runs;
    private readonly IDocumentedScreens _documented;
    private readonly IScreenComparison _comparison;

    public ScreensCommandService(
        IProductScreenCatalog catalog,
        IScreenCaptureStore store,
        IScreenSurfaceHost host,
        Func<string, IProductScreenSurface> surfaces,
        IScreenRunStore runs,
        IDocumentedScreens documented,
        IScreenComparison comparison)
    {
        _catalog = catalog;
        _store = store;
        _host = host;
        _surfaces = surfaces;
        _runs = runs;
        _documented = documented;
        _comparison = comparison;
    }

    public async Task<CommandResultDto> CaptureAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            return await CaptureCoreAsync(command, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s capturing product screens.");
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                        or IOException
                                        or JsonException
                                        or HttpRequestException
                                        or WebSocketException
                                        or UriFormatException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }

    private async Task<CommandResultDto> CaptureCoreAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var captures = new List<ScreenCaptureDto>();
        await using (_host)
        {
            foreach (var surface in SelectedSurfaces(command))
                await foreach (var capture in CaptureSurfaceAsync(surface, command.View, cancellationToken))
                    captures.Add(capture);
        }

        var manifest = _store.WriteManifest(new ScreenRunManifestDto(
            DebugLayout.DemoServerName,
            DebugLayout.DemoWorkspaceId,
            captures));
        return CommandResultDto.Ok(Summary(captures), _store.Relative(manifest));
    }

    /// <summary>
    /// Drives one surface through its routes, reporting each screen as it is taken.
    /// </summary>
    /// <remarks>
    /// Streamed rather than collected because the routes are cumulative and sequential: each
    /// screen is captured in the state the ones before it left behind, so there is nothing to
    /// run in parallel and a caller watching the run learns where it got to.
    /// </remarks>
    private async IAsyncEnumerable<ScreenCaptureDto> CaptureSurfaceAsync(
        string surface,
        string? view,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var screens = Selected(surface, view);
        _store.Reset(surface);
        await _host.StartAsync(surface, cancellationToken);
        await using var driver = _surfaces(surface);
        await driver.OpenAsync(cancellationToken);

        string? previous = null;
        foreach (var screen in screens)
        {
            var capture = await CaptureScreenAsync(driver, screen, previous, cancellationToken);
            if (capture.File is not null)
                previous = _store.CapturePath(screen.Surface, screen.View);
            yield return capture;
        }
    }

    private async Task<ScreenCaptureDto> CaptureScreenAsync(
        IProductScreenSurface driver,
        ProductScreenDto screen,
        string? previousCapture,
        CancellationToken cancellationToken)
    {
        if (!screen.Available)
            return new ScreenCaptureDto(screen.Id, screen.Surface, screen.View, screen.Title, null, screen.UnavailableReason);

        await driver.RunAsync(screen.Steps, cancellationToken);
        var path = _store.CapturePath(screen.Surface, screen.View);
        await driver.CaptureAsync(path, cancellationToken);
        if (_store.IsUnchangedFrom(path, previousCapture))
        {
            throw new InvalidOperationException(
                $"{screen.Id} came out identical to the screen before it, so its steps changed nothing on the client. "
                + "A Desktop layout change moves the points in DesktopScreenGeometry; re-read them against a 1440x900 Demo session.");
        }

        var text = await driver.ReadTextAsync(cancellationToken);
        return new ScreenCaptureDto(screen.Id, screen.Surface, screen.View, screen.Title, _store.Relative(path), null, text);
    }


    /// <summary>
    /// Reports where the screens the design system documents and the real ones diverge.
    /// </summary>
    /// <remarks>
    /// This reads the two manifests and starts nothing, so it is cheap enough to run after every
    /// capture. It is the half that was missing: both sides were being photographed and neither
    /// was ever held against the other, which left "match the design system to the app" as a job
    /// for whoever remembered to open both folders.
    /// </remarks>
    public async Task<CommandResultDto> CompareAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            return await Task.Run(() => CompareCore(command), timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s comparing product screens.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or JsonException)
        {
            return CommandResultDto.Fail(ex.Message);
        }
    }

    private CommandResultDto CompareCore(DebugCommandDto command)
    {
        var run = _runs.Read()
                  ?? throw new InvalidOperationException(
                      "No product screens run to compare. Run au-debug screens first.");

        var documented = _documented.Read().Where(scene => Includes(command, scene.Surface, scene.Id)).ToArray();
        if (documented.Length == 0)
            throw new InvalidOperationException(UnknownComparison(command));

        var results = _comparison.Compare(documented, run.Screens);
        var diverged = results.Where(result => result.Status == ScreenComparisonStatus.Diverged).ToArray();
        var absent = results.Where(result => result.Status == ScreenComparisonStatus.NotCaptured).ToArray();

        if (diverged.Length > 0 || absent.Length > 0)
            return CommandResultDto.Fail(Report(results, diverged, absent));

        return CommandResultDto.Ok(Report(results, diverged, absent));
    }

    private static bool Includes(DebugCommandDto command, string surface, string id)
    {
        // Surface carries the scope; Action is "compare" for every one of these.
        if (command.Surface is not null && surface != command.Surface) return false;
        if (string.IsNullOrWhiteSpace(command.View)) return true;
        return id == command.View || id.EndsWith($"-{command.View}", StringComparison.Ordinal);
    }

    private static string UnknownComparison(DebugCommandDto command)
        => string.IsNullOrWhiteSpace(command.View)
            ? $"Error: the design system documents no {command.Surface} screens."
            : $"Error: the design system documents no screen '{command.View}'.";

    private static string Report(
        IReadOnlyList<ScreenComparisonDto> results,
        IReadOnlyList<ScreenComparisonDto> diverged,
        IReadOnlyList<ScreenComparisonDto> absent)
    {
        var matched = results.Count(result => result.Status == ScreenComparisonStatus.Matched);
        var lines = new List<string>
        {
            $"Compared {results.Count} documented screen(s) with the last run: {matched} matched, "
            + $"{diverged.Count} diverged, {absent.Count} not captured."
        };
        foreach (var result in absent)
            lines.Add($"{result.Id}: {result.Detail}");
        foreach (var result in diverged)
            lines.Add($"{result.Id}: the real screen is missing {Quote(result.MissingCopy)}.");
        foreach (var result in results.Where(entry => entry.Status is ScreenComparisonStatus.Skipped or ScreenComparisonStatus.NotComparable))
            lines.Add($"{result.Id}: {result.Detail}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Quote(IReadOnlyList<string> copy)
        => string.Join(", ", copy.Select(entry => $"'{entry}'"));

    private IReadOnlyList<string> SelectedSurfaces(DebugCommandDto command)
        => command.Action is ProductSurface.Desktop or ProductSurface.Mobile
            ? [command.Action]
            : _catalog.Surfaces;

    private IReadOnlyList<ProductScreenDto> Selected(string surface, string? view)
    {
        var screens = _catalog.Screens(surface);
        if (string.IsNullOrWhiteSpace(view))
            return screens;

        var selected = screens.Where(screen => screen.View == view || screen.Id == view).ToArray();
        if (selected.Length == 0)
            throw new InvalidOperationException(UnknownView(surface, view, screens));
        return selected;
    }

    private static string UnknownView(string surface, string view, IReadOnlyList<ProductScreenDto> screens)
        => $"Error: unknown {surface} screen '{view}'. Choose one of: "
           + string.Join(", ", screens.Select(screen => screen.View));

    private static string Summary(IReadOnlyList<ScreenCaptureDto> captures)
    {
        var written = captures.Where(capture => capture.File is not null).ToArray();
        var skipped = captures.Where(capture => capture.File is null).ToArray();
        var lines = new List<string>
        {
            $"Captured {written.Length} product screen(s) from the Demo server into {DebugLayout.ProductScreensDirectory}/."
        };
        lines.AddRange(skipped.Select(capture => $"skipped {capture.Id}: {capture.SkippedReason}"));
        return string.Join(Environment.NewLine, lines);
    }
}
