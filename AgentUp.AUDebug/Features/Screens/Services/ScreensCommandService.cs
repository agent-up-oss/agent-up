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

    public ScreensCommandService(
        IProductScreenCatalog catalog,
        IScreenCaptureStore store,
        IScreenSurfaceHost host,
        Func<string, IProductScreenSurface> surfaces)
    {
        _catalog = catalog;
        _store = store;
        _host = host;
        _surfaces = surfaces;
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

        foreach (var screen in screens)
            yield return await CaptureScreenAsync(driver, screen, cancellationToken);
    }

    private async Task<ScreenCaptureDto> CaptureScreenAsync(
        IProductScreenSurface driver,
        ProductScreenDto screen,
        CancellationToken cancellationToken)
    {
        if (!screen.Available)
            return new ScreenCaptureDto(screen.Id, screen.Surface, screen.View, screen.Title, null, screen.UnavailableReason);

        await driver.RunAsync(screen.Steps, cancellationToken);
        var path = _store.CapturePath(screen.Surface, screen.View);
        await driver.CaptureAsync(path, cancellationToken);
        return new ScreenCaptureDto(screen.Id, screen.Surface, screen.View, screen.Title, _store.Relative(path), null);
    }

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
