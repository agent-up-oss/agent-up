using System.Collections.Concurrent;
using System.Text.Json;
using AgentUp.Server.Features.Browser.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class HeadlessBrowserSessionManager(
    string chromiumDir,
    string profilesDir,
    ScreencastBroadcastService broadcast,
    ILogger<HeadlessBrowserSessionManager> logger,
    string? configuredExecutablePath = null)
    : IHostedService
{
    private readonly ConcurrentDictionary<string, BrowserSessionState> _sessions = new();
    private readonly ConcurrentDictionary<string, BrowserControlMode> _controlModes = new();
    private readonly SemaphoreSlim _createLock = new(1, 1);
    private CancellationTokenSource _stopCts = new();
    private string? _executablePath;
    private bool _chromiumReady;

    public Task StartAsync(CancellationToken ct)
    {
        _stopCts = new CancellationTokenSource();
        return Task.CompletedTask;
    }

    private async Task EnsureChromiumAsync(CancellationToken ct)
    {
        if (_chromiumReady) return;
        if (!string.IsNullOrWhiteSpace(configuredExecutablePath))
        {
            _executablePath = configuredExecutablePath;
            _chromiumReady = true;
            return;
        }
        logger.LogInformation("Downloading Chromium to {ChromiumDir}…", chromiumDir);
        try
        {
            var fetcher = new BrowserFetcher(new BrowserFetcherOptions { Path = chromiumDir });
            var revision = await fetcher.DownloadAsync().WaitAsync(ct);
            _executablePath = revision.GetExecutablePath();
            logger.LogInformation("Chromium ready at {Path}", _executablePath);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Chromium download failed; will attempt to use system Chromium.");
        }
        _chromiumReady = true;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _stopCts.CancelAsync();
        var sessions = _sessions.Values.ToArray();
        _sessions.Clear();
        foreach (var session in sessions)
        {
            try { await session.Browser.DisposeAsync(); }
            catch (Exception ex) when (ex is PuppeteerException or ObjectDisposedException)
            {
                logger.LogWarning(ex, "Error disposing browser for workspace {WorkspaceId}.", session.WorkspaceId);
            }
        }
        _stopCts.Dispose();
        _createLock.Dispose();
    }

    public async Task<BrowserSessionState> EnsureSessionAsync(string workspaceId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(workspaceId, out var fast) && fast.Browser.IsConnected)
            return fast;

        await _createLock.WaitAsync(ct);
        try
        {
            if (_sessions.TryGetValue(workspaceId, out var locked) && locked.Browser.IsConnected)
                return locked;

            await EnsureChromiumAsync(ct);

            if (_sessions.TryGetValue(workspaceId, out var stale))
            {
                _sessions.TryRemove(workspaceId, out _);
                try { await stale.Browser.DisposeAsync(); }
                catch (Exception ex) when (ex is PuppeteerException or ObjectDisposedException)
                {
                    logger.LogDebug(ex, "Error disposing stale browser for workspace {WorkspaceId}.", workspaceId);
                }
            }

            var session = await CreateSessionAsync(workspaceId, ct);
            _sessions[workspaceId] = session;
            return session;
        }
        finally
        {
            _createLock.Release();
        }
    }

    public BrowserSessionState? GetSession(string workspaceId)
        => _sessions.TryGetValue(workspaceId, out var session) ? session : null;

    public BrowserControlMode GetControlMode(string workspaceId)
        => _controlModes.TryGetValue(workspaceId, out var mode) ? mode : BrowserControlMode.DefaultAi;

    public ControlModeDto GetControlModeDto(string workspaceId)
    {
        var m = GetControlMode(workspaceId);
        return new ControlModeDto(m.Authority.ToString().ToLowerInvariant(), m.Width, m.Height);
    }

    public async Task<BrowserControlMode> SetControlModeAsync(
        string workspaceId, BrowserControlMode mode, CancellationToken ct)
    {
        _controlModes[workspaceId] = mode;
        if (mode.Authority == ControlAuthority.Ai && mode.Width > 0 && mode.Height > 0)
            await SetViewportAsync(workspaceId, mode.Width, mode.Height, ct);
        var msg = JsonSerializer.Serialize(new
        {
            type = "mode",
            authority = mode.Authority == ControlAuthority.Ai ? "ai" : "human",
            width = mode.Width,
            height = mode.Height
        });
        await broadcast.BroadcastTextAsync(workspaceId, msg, ct);
        return mode;
    }

    public async Task<(bool Success, string? Error)> TrySetControlModeAsync(
        string workspaceId, string authority, int width, int height, CancellationToken ct)
    {
        if (!Enum.TryParse<ControlAuthority>(authority, ignoreCase: true, out var parsed))
            return (false, $"Invalid authority '{authority}'. Use 'human' or 'ai'.");
        if (parsed == ControlAuthority.Ai && !BrowserControlMode.AllowedWidths.Contains(width))
            return (false, $"Width {width} is not allowed. Use one of: {string.Join(", ", BrowserControlMode.AllowedWidths)}.");
        if (parsed == ControlAuthority.Ai && !BrowserControlMode.AllowedHeights.Contains(height))
            return (false, $"Height {height} is not allowed. Use one of: {string.Join(", ", BrowserControlMode.AllowedHeights)}.");
        var mode = parsed == ControlAuthority.Ai
            ? new BrowserControlMode(ControlAuthority.Ai, width, height)
            : BrowserControlMode.DefaultHuman;
        await SetControlModeAsync(workspaceId, mode, ct);
        return (true, null);
    }

    public async Task<bool> TrySetViewportAsync(string workspaceId, int width, int height, CancellationToken ct)
    {
        if (GetControlMode(workspaceId).Authority != ControlAuthority.Human) return false;
        await SetViewportAsync(workspaceId, width, height, ct);
        return true;
    }

    public async Task SetViewportAsync(string workspaceId, int width, int height, CancellationToken ct)
    {
        var session = GetSession(workspaceId);
        if (session is null) return;
        try { await session.Page.SetViewportAsync(new ViewPortOptions { Width = width, Height = height }).WaitAsync(ct); }
        catch (Exception ex) when (ex is PuppeteerException or OperationCanceledException)
        {
            logger.LogDebug(ex, "Viewport resize failed for workspace {WorkspaceId}.", workspaceId);
        }
    }

    public async Task DisposeSessionAsync(string workspaceId)
    {
        if (!_sessions.TryRemove(workspaceId, out var session)) return;
        try { await session.Browser.DisposeAsync(); }
        catch (Exception ex) when (ex is PuppeteerException or ObjectDisposedException)
        {
            logger.LogWarning(ex, "Error disposing browser for workspace {WorkspaceId}.", workspaceId);
        }
    }

    private async Task<BrowserSessionState> CreateSessionAsync(string workspaceId, CancellationToken ct)
    {
        var profilePath = Path.Join(profilesDir, workspaceId);
        Directory.CreateDirectory(profilePath);

        var options = new LaunchOptions
        {
            Headless = true,
            HeadlessMode = HeadlessMode.Shell,
            Pipe = false,
            UserDataDir = profilePath,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
            ]
        };

        if (_executablePath is not null)
            options.ExecutablePath = _executablePath;

        logger.LogInformation("Launching Chromium for workspace {WorkspaceId}.", workspaceId);
        var browser = await Puppeteer.LaunchAsync(options).WaitAsync(ct);
        var page = await browser.NewPageAsync().WaitAsync(ct);
        await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 720 }).WaitAsync(ct);

        _ = Task.Run(() => RunScreencastLoopAsync(workspaceId, page, _stopCts.Token));

        return new BrowserSessionState(workspaceId, browser, page);
    }

    private async Task RunScreencastLoopAsync(string workspaceId, IPage page, CancellationToken ct)
    {
        var options = new ScreenshotOptions { Type = ScreenshotType.Jpeg, Quality = 75, FullPage = false };
        while (!ct.IsCancellationRequested)
        {
            if (!broadcast.HasSubscribers(workspaceId))
            {
                try { await Task.Delay(500, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                continue;
            }

            try
            {
                var frame = await page.ScreenshotDataAsync(options);
                if (frame.Length > 0)
                    await broadcast.BroadcastFrameAsync(workspaceId, frame, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is PuppeteerException or InvalidOperationException)
            {
                logger.LogDebug(ex, "Screencast frame error for workspace {WorkspaceId}.", workspaceId);
            }

            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
