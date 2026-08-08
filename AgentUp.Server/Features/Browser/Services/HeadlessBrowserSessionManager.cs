using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using AgentUp.Server.Features.Browser.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class HeadlessBrowserSessionManager(
    string chromiumDir,
    string profilesDir,
    BrowserRemoteDisplayService display,
    WorkspaceStreamStateService streamState,
    ILogger<HeadlessBrowserSessionManager> logger,
    string? configuredExecutablePath = null)
    : IHostedService
{
    private readonly ConcurrentDictionary<string, BrowserSessionState> _sessions = new();
    private readonly ConcurrentDictionary<string, BrowserControlMode> _controlModes = new();
    private readonly ConcurrentDictionary<string, BrowserViewportPreset> _aiViewportPresets = new();
    private readonly SemaphoreSlim _createLock = new(1, 1);
    private CancellationTokenSource _stopCts = new();
    private string? _executablePath;
    private bool _chromiumReady;
    private int _stopCalled;
    private volatile string _chromiumDownloadState = "not_started";
    private int _chromiumDownloadProgress; // Volatile.Read/Write
    private int _lastPublishedProgress = -1;
    private TaskCompletionSource? _chromiumTcs;

    private static string Sanitize(string id) =>
        id.Replace("\r", string.Empty, StringComparison.Ordinal)
          .Replace("\n", string.Empty, StringComparison.Ordinal);

    public (string State, int Progress) GetChromiumStatus()
        => (_chromiumDownloadState, Volatile.Read(ref _chromiumDownloadProgress));

    public Task StartAsync(CancellationToken ct)
    {
        _stopCts = new CancellationTokenSource();
        Interlocked.Exchange(ref _stopCalled, 0);
        if (!_chromiumReady)
        {
            _chromiumTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Task.Run(() => RunChromiumDownloadAsync(_stopCts.Token));
        }
        return Task.CompletedTask;
    }

    private async Task EnsureChromiumAsync(CancellationToken ct)
    {
        if (_chromiumReady) return;
        if (_chromiumTcs is { Task: var t })
        {
            try { await t.WaitAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Chromium download failed; proceeding with system Chromium.");
            }
            return;
        }
        await RunChromiumDownloadAsync(ct);
    }

    private async Task RunChromiumDownloadAsync(CancellationToken ct)
    {
        if (_chromiumReady)
        {
            _chromiumTcs?.TrySetResult();
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuredExecutablePath))
        {
            _executablePath = configuredExecutablePath;
            _chromiumDownloadState = "ready";
            Volatile.Write(ref _chromiumDownloadProgress, 100);
            _chromiumReady = true;
            streamState.OnChromiumStateChanged("ready", 100);
            _chromiumTcs?.TrySetResult();
            return;
        }

        try
        {
            var checkFetcher = new BrowserFetcher(new BrowserFetcherOptions { Path = chromiumDir });
            var installed = checkFetcher.GetInstalledBrowsers().FirstOrDefault();
            if (installed is not null)
            {
                _executablePath = installed.GetExecutablePath();
                logger.LogInformation("Chromium already cached at {Path}", _executablePath);
                _chromiumDownloadState = "ready";
                Volatile.Write(ref _chromiumDownloadProgress, 100);
                _chromiumReady = true;
                streamState.OnChromiumStateChanged("ready", 100);
                _chromiumTcs?.TrySetResult();
                return;
            }

            logger.LogInformation("Downloading Chromium to {ChromiumDir}…", chromiumDir);
            _chromiumDownloadState = "downloading";
            streamState.OnChromiumStateChanged("downloading", 0);

            var fetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
                Path = chromiumDir,
                CustomFileDownload = (address, fileName) => DownloadWithProgressAsync(address, fileName, ct)
            });
            var revision = await fetcher.DownloadAsync().WaitAsync(ct);
            _executablePath = revision.GetExecutablePath();
            logger.LogInformation("Chromium ready at {Path}", _executablePath);
            Volatile.Write(ref _chromiumDownloadProgress, 100);
            _chromiumDownloadState = "ready";
            _chromiumReady = true;
            streamState.OnChromiumStateChanged("ready", 100);
            _chromiumTcs?.TrySetResult();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _chromiumDownloadState = "failed";
            streamState.OnChromiumStateChanged("failed", 0);
            _chromiumTcs?.TrySetCanceled(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Chromium download failed; will attempt to use system Chromium.");
            _chromiumDownloadState = "failed";
            _chromiumReady = true;
            streamState.OnChromiumStateChanged("failed", 0);
            _chromiumTcs?.TrySetResult();
        }
    }

    private async Task DownloadWithProgressAsync(string address, string fileName, CancellationToken ct)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        var buffer = new byte[65536];
        long downloaded = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (total > 0)
            {
                var pct = (int)(downloaded * 100L / total);
                Volatile.Write(ref _chromiumDownloadProgress, pct);
                if (pct >= _lastPublishedProgress + 5)
                {
                    _lastPublishedProgress = pct;
                    streamState.OnChromiumStateChanged("downloading", pct);
                }
            }
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _stopCalled, 1) != 0) return;
        await _stopCts.CancelAsync();
        var sessions = _sessions.Values.ToArray();
        _sessions.Clear();
        foreach (var session in sessions)
        {
            try { await session.Browser.DisposeAsync(); }
            catch (Exception ex) when (ex is PuppeteerException or ObjectDisposedException)
            {
                logger.LogWarning(ex, "Error disposing browser for workspace {WorkspaceId}.", Sanitize(session.WorkspaceId));
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
                    logger.LogDebug(ex, "Error disposing stale browser for workspace {WorkspaceId}.", Sanitize(workspaceId));
                }
            }

            var session = await CreateSessionAsync(workspaceId, ct);
            _sessions[workspaceId] = session;
            streamState.OnSessionActive(workspaceId);
            return session;
        }
        finally
        {
            _createLock.Release();
        }
    }

    public async Task StreamDisplayAsync(
        string workspaceId,
        WebSocket ws,
        Func<string, Task> inputCallback,
        CancellationToken ct)
    {
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        sessionCts.CancelAfter(TimeSpan.FromSeconds(10));
        try { await EnsureSessionAsync(workspaceId, sessionCts.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Session bootstrap timed out or failed for {WorkspaceId}; viewer will retry.", workspaceId);
        }
        await display.ConnectAsync(workspaceId, ws, inputCallback, ct);
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
        await display.BroadcastTextAsync(workspaceId, msg, ct);
        return mode;
    }

    public async Task<(bool Success, string? Error)> TrySetControlModeAsync(
        string workspaceId, string authority, string? preset, int width, int height, CancellationToken ct)
    {
        if (!Enum.TryParse<ControlAuthority>(authority, ignoreCase: true, out var parsed))
            return (false, $"Invalid authority '{authority}'. Use 'human' or 'ai'.");

        string? error = null;
        var mode = parsed == ControlAuthority.Ai
            ? ResolveAiMode(workspaceId, preset, width, height, out error)
            : BrowserControlMode.DefaultHuman;
        if (mode is null)
            return (false, error);

        await SetControlModeAsync(workspaceId, mode, ct);
        return (true, null);
    }

    private BrowserControlMode? ResolveAiMode(
        string workspaceId, string? presetId, int width, int height, out string? error)
    {
        var preset = !string.IsNullOrWhiteSpace(presetId)
            ? BrowserViewportPreset.Find(presetId)
            : BrowserViewportPreset.Find(width, height)
              ?? (_aiViewportPresets.TryGetValue(workspaceId, out var remembered)
                  ? remembered
                  : BrowserViewportPreset.Default);

        if (preset is null)
        {
            error = $"Viewport preset '{presetId}' is not allowed. Use one of: {string.Join(", ", BrowserViewportPreset.Standard.Select(p => p.Id))}.";
            return null;
        }

        _aiViewportPresets[workspaceId] = preset;
        error = null;
        return new BrowserControlMode(ControlAuthority.Ai, preset.Width, preset.Height);
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
        try { await session.SetViewportAsync(new ViewPortOptions { Width = width, Height = height }, ct); }
        catch (Exception ex) when (ex is PuppeteerException or OperationCanceledException)
        {
            logger.LogDebug(ex, "Viewport resize failed for workspace {WorkspaceId}.", Sanitize(workspaceId));
        }
    }

    public async Task<byte[]?> CaptureDisplayFrameAsync(string workspaceId, CancellationToken ct)
    {
        var session = GetSession(workspaceId);
        if (session is null) return null;

        try
        {
            var frame = await session.Page.ScreenshotDataAsync(DisplayScreenshotOptions).WaitAsync(ct);
            if (frame.Length == 0) return null;
            await display.BroadcastFrameAsync(workspaceId, frame, ct);
            return frame;
        }
        catch (Exception ex) when (ex is PuppeteerException or InvalidOperationException or OperationCanceledException)
        {
            logger.LogDebug(ex, "RDP display frame capture failed for workspace {WorkspaceId}.", Sanitize(workspaceId));
            return null;
        }
    }

    public async Task DisposeSessionAsync(string workspaceId)
    {
        if (!_sessions.TryRemove(workspaceId, out var session)) return;
        streamState.OnSessionInactive(workspaceId);
        try { await session.Browser.DisposeAsync(); }
        catch (Exception ex) when (ex is PuppeteerException or ObjectDisposedException)
        {
            logger.LogWarning(ex, "Error disposing browser for workspace {WorkspaceId}.", Sanitize(workspaceId));
        }
    }

    private async Task<BrowserSessionState> CreateSessionAsync(string workspaceId, CancellationToken ct)
    {
        if (!Guid.TryParse(workspaceId, out _))
            throw new ArgumentException("Workspace ID must be a valid GUID.", nameof(workspaceId));
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

        logger.LogInformation("Launching Chromium for workspace {WorkspaceId}.", Sanitize(workspaceId));
        var browser = await Puppeteer.LaunchAsync(options).WaitAsync(ct);
        var page = await browser.NewPageAsync().WaitAsync(ct);
        await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 720 }).WaitAsync(ct);

        var session = new BrowserSessionState(workspaceId, browser, page);
        _ = Task.Run(() => RunRemoteDisplayLoopAsync(session, _stopCts.Token));

        return session;
    }

    private async Task RunRemoteDisplayLoopAsync(BrowserSessionState session, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && session.Browser.IsConnected)
        {
            if (!display.HasSubscribers(session.WorkspaceId))
            {
                try { await Task.Delay(500, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                continue;
            }

            try
            {
                var frame = await session.Page.ScreenshotDataAsync(DisplayScreenshotOptions);
                if (frame.Length > 0)
                    await display.BroadcastFrameAsync(session.WorkspaceId, frame, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is PuppeteerException or InvalidOperationException)
            {
                logger.LogDebug(ex, "RDP bitmap frame error for workspace {WorkspaceId}.", Sanitize(session.WorkspaceId));
            }

            var delay = display.HasActiveInput(session.WorkspaceId) ? 50 : 200;
            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }

    private static readonly ScreenshotOptions DisplayScreenshotOptions = new()
    {
        Type = ScreenshotType.Jpeg,
        Quality = 75,
        FullPage = false
    };
}
