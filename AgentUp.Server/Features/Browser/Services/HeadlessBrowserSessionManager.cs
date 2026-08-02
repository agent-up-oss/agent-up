using System.Collections.Concurrent;
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
    private readonly SemaphoreSlim _createLock = new(1, 1);
    private CancellationTokenSource _stopCts = new();
    private string? _executablePath;

    public async Task StartAsync(CancellationToken ct)
    {
        _stopCts = new CancellationTokenSource();

        if (!string.IsNullOrWhiteSpace(configuredExecutablePath))
        {
            _executablePath = configuredExecutablePath;
            logger.LogInformation("Using configured Chromium at {Path}", _executablePath);
            return;
        }

        logger.LogInformation("Downloading Chromium to {ChromiumDir}…", chromiumDir);
        try
        {
            var fetcher = new BrowserFetcher(new BrowserFetcherOptions { Path = chromiumDir });
            var revision = await fetcher.DownloadAsync();
            _executablePath = revision.GetExecutablePath();
            logger.LogInformation("Chromium ready at {Path}", _executablePath);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Chromium download failed; will attempt to use system Chromium.");
        }
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
