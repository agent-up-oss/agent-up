using System.Diagnostics;
using System.Text.Json;
using AgentUp.Desktop.Features.Browser.Models;
using AgentUp.Desktop.Features.Browser.Providers;
using AgentUp.Desktop.Features.Browser.Resources;
using AgentUp.Desktop.Shared.Interfaces;
using Avalonia.Threading;

namespace AgentUp.Desktop.Features.Browser.Services;

internal sealed class BrowserCommandPoller
{
    private const int ScreenshotWidth = 800;
    private const int ScreenshotHeight = 450;
    private static readonly TimeSpan PageStateSettleTimeout = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan PageStateSettleInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ClickTabActivationDelay = TimeSpan.FromMilliseconds(200);
    private CancellationTokenSource? _cts;
    private readonly BrowserCommandHttpClient _client;
    private readonly IBrowserWindowHost _host;
    private readonly Func<int, CancellationToken, Task> _delay;

    public BrowserCommandPoller(BrowserCommandHttpClient client, IBrowserWindowHost host)
        : this(client, host, DelayAsync)
    {
    }

    internal BrowserCommandPoller(
        BrowserCommandHttpClient client,
        IBrowserWindowHost host,
        Func<int, CancellationToken, Task> delay)
    {
        _client = client;
        _host = host;
        _delay = delay;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ids = await _host.GetActiveWorkspaceIdsAsync();
            if (ids.Count == 0)
            {
                await DelayAsync(500, ct);
                continue;
            }

            BrowserCommandDto? command = null;
            try
            {
                command = await _client.GetPendingCommandAsync(ids, timeoutMs: 5000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
            {
                Trace.TraceWarning($"[BrowserCommandPoller] Poll error: {ex.Message}");
                await DelayAsync(1000, ct);
                continue;
            }

            if (command is null) continue;

            var result = await ExecuteAsync(command, ct);
            await PostResultSafe(result, ct);
        }
    }

    internal async Task<BrowserCommandResultDto> ExecuteAsync(BrowserCommandDto command, CancellationToken ct)
    {
        try
        {
            return command.Kind switch
            {
                BrowserCommandKind.Navigate => await AttachPageStateAsync(await NavigateAsync(command, ct), command, ct),
                BrowserCommandKind.InspectPage => await InspectAsync(command),
                BrowserCommandKind.Click => await AttachPageStateAsync(await ClickAsync(command, ct), command, ct),
                BrowserCommandKind.Fill => await AttachPageStateAsync(await EvalCommandAsync(command, BrowserScripts.Fill(command.Selector!, command.Text!)), command, ct),
                BrowserCommandKind.Press => await AttachPageStateAsync(await EvalCommandAsync(command, BrowserScripts.Press(command.Key!)), command, ct),
                BrowserCommandKind.WaitForSelector => await AttachPageStateAsync(await WaitForConditionAsync(command, BrowserScripts.CheckSelector(command.Selector!), ct), command, ct),
                BrowserCommandKind.WaitForText => await AttachPageStateAsync(await WaitForConditionAsync(command, BrowserScripts.CheckText(command.Text!), ct), command, ct),
                BrowserCommandKind.WaitForNavigation => await AttachPageStateAsync(await WaitForNavigationAsync(command, ct), command, ct),
                BrowserCommandKind.Screenshot => await ScreenshotAsync(command, ct),
                _ => Fail(command, $"Unknown command kind: {command.Kind}")
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or HttpRequestException or IOException)
        {
            return Fail(command, ex.Message);
        }
    }

    private async Task<BrowserCommandResultDto> NavigateAsync(BrowserCommandDto command, CancellationToken ct)
    {
        var navigated = await Dispatcher.UIThread.InvokeAsync(() => _host.NavigateTo(command.WorkspaceId, command.Url));
        return navigated
            ? Ok(command)
            : Fail(command, $"Could not navigate workspace '{command.WorkspaceId}' to '{command.Url}'.");
    }

    private async Task<BrowserCommandResultDto> InspectAsync(BrowserCommandDto command)
        => await EvalCommandAsync(command, BrowserScripts.InspectPage);

    private async Task<BrowserCommandResultDto> ClickAsync(BrowserCommandDto command, CancellationToken ct)
    {
        var target = await ReadClickTargetAsync(command);
        if (!target.Success)
            return Fail(command, target.Error ?? $"Element not found: {command.Selector}");

        if (!string.IsNullOrWhiteSpace(target.Url))
        {
            await _host.ActivateWorkspaceUrlAsync(command.WorkspaceId, target.Url);
            await _delay((int)ClickTabActivationDelay.TotalMilliseconds, ct);
        }

        return await EvalCommandAsync(command, BrowserScripts.AnimatedClick(command.Selector!));
    }

    private async Task<ClickTargetDto> ReadClickTargetAsync(BrowserCommandDto command)
    {
        var data = await _host.EvalAsync(command.WorkspaceId, BrowserScripts.ClickTarget(command.Selector!));
        if (data is null)
            return new ClickTargetDto(false, null, $"No browser session found for workspace '{command.WorkspaceId}'.");

        try
        {
            var target = JsonSerializer.Deserialize<ClickTargetDto>(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return target ?? new ClickTargetDto(false, null, "Desktop returned invalid click target data.");
        }
        catch (JsonException)
        {
            return new ClickTargetDto(false, null, "Desktop returned invalid click target data.");
        }
    }

    private async Task<BrowserCommandResultDto> EvalCommandAsync(BrowserCommandDto command, string script)
    {
        var data = await _host.EvalAsync(command.WorkspaceId, script);
        return data is not null
            ? new BrowserCommandResultDto(command.CommandId, true, data, null)
            : Fail(command, $"No browser session found for workspace '{command.WorkspaceId}'.");
    }

    internal async Task<BrowserCommandResultDto> AttachPageStateAsync(
        BrowserCommandResultDto result,
        BrowserCommandDto command,
        CancellationToken ct = default)
    {
        if (!result.Success) return result;
        var state = await ReadSettledPageStateAsync(command.WorkspaceId, ct);
        return state is null
            ? result
            : new BrowserCommandResultDto(command.CommandId, true, state, null);
    }

    private async Task<string?> ReadSettledPageStateAsync(string workspaceId, CancellationToken ct)
    {
        string? previous = null;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < PageStateSettleTimeout && !ct.IsCancellationRequested)
        {
            var current = await _host.EvalAsync(workspaceId, BrowserScripts.InspectPage);
            if (current is not null && string.Equals(current, previous, StringComparison.Ordinal))
                return current;

            previous = current;
            await DelayAsync((int)PageStateSettleInterval.TotalMilliseconds, ct);
        }

        return previous;
    }

    private async Task<BrowserCommandResultDto> WaitForConditionAsync(BrowserCommandDto command, string conditionScript, CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(command.TimeoutMs);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
        {
            var result = await _host.EvalAsync(command.WorkspaceId, conditionScript);
            if (result is "true") return Ok(command);
            await _delay(200, ct);
        }

        return Fail(command, $"Condition not met within {command.TimeoutMs} ms.");
    }

    private async Task<BrowserCommandResultDto> WaitForNavigationAsync(BrowserCommandDto command, CancellationToken ct)
    {
        // Give the browser a moment to start navigating before we poll readyState.
        await _delay(200, ct);

        var timeout = TimeSpan.FromMilliseconds(command.TimeoutMs);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
        {
            var state = await _host.EvalAsync(command.WorkspaceId, BrowserScripts.CheckNavigation);
            if (state == "complete") return Ok(command);
            await _delay(200, ct);
        }

        // Treat timeout as soft success — the page may still be loading but interaction can proceed.
        return Ok(command);
    }

    private async Task<BrowserCommandResultDto> ScreenshotAsync(BrowserCommandDto command, CancellationToken ct)
    {
        var url = await _host.EvalAsync(command.WorkspaceId, BrowserScripts.GetUrl);
        if (url is null)
            return Fail(command, $"No browser session found for workspace '{command.WorkspaceId}'.");

        var path = Path.Join(Path.GetTempPath(), $"agentup-screenshot-{Guid.NewGuid():N}.png");

        foreach (var browser in new[] { "chromium", "google-chrome", "chromium-browser" })
        {
            try
            {
                var psi = new ProcessStartInfo(browser) { UseShellExecute = false };
                psi.ArgumentList.Add("--headless=new");
                psi.ArgumentList.Add("--no-sandbox");
                psi.ArgumentList.Add("--disable-gpu");
                psi.ArgumentList.Add("--disable-dev-shm-usage");
                psi.ArgumentList.Add("--run-all-compositor-stages-before-draw");
                psi.ArgumentList.Add("--virtual-time-budget=3000");
                psi.ArgumentList.Add($"--screenshot={path}");
                psi.ArgumentList.Add($"--window-size={ScreenshotWidth},{ScreenshotHeight}");
                psi.ArgumentList.Add(url);

                using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process.");
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    KillProcess(process, browser);
                    return Fail(command, $"Screenshot failed: {browser} did not finish within 15 seconds.");
                }

                if (!File.Exists(path))
                    return Fail(command, $"Screenshot failed: {browser} exited without producing a file.");

                var bytes = await File.ReadAllBytesAsync(path, ct);
                File.Delete(path);
                var payload = new BrowserScreenshotResultDto(
                    url,
                    "image/png",
                    Convert.ToBase64String(bytes),
                    ScreenshotWidth,
                    ScreenshotHeight);
                return new BrowserCommandResultDto(
                    command.CommandId,
                    true,
                    JsonSerializer.Serialize(payload),
                    null);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Trace.TraceInformation($"[BrowserCommandPoller] Browser executable not found: {browser}");
            }
        }

        return Fail(command, "Screenshot failed: no suitable browser found (tried chromium, google-chrome, chromium-browser).");
    }

    private async Task PostResultSafe(BrowserCommandResultDto result, CancellationToken ct)
    {
        try
        {
            await _client.PostCommandResultAsync(result, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or IOException or InvalidOperationException)
        {
            Trace.TraceWarning($"[BrowserCommandPoller] Result post error: {ex.Message}");
        }
    }

    private static BrowserCommandResultDto Ok(BrowserCommandDto command) =>
        new(command.CommandId, true, null, null);

    private static BrowserCommandResultDto Fail(BrowserCommandDto command, string error) =>
        new(command.CommandId, false, null, error);

    private static void KillProcess(Process process, string browser)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Trace.TraceWarning($"[BrowserCommandPoller] Failed to kill timed-out {browser}: {ex.Message}");
        }
    }

    private static async Task DelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
    }

}
