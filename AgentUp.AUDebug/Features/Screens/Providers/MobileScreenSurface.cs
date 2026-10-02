using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// Drives the hosted Mobile web client in headless Chromium at phone metrics.
/// </summary>
/// <remarks>
/// One document load per run: <see cref="OpenAsync"/> activates Demo and every later step
/// routes inside the app, because the Demo backend lives in the page and a reload would
/// discard the state the route has built.
/// </remarks>
public sealed class MobileScreenSurface : IProductScreenSurface
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;
    private readonly int _debuggingPort;
    private System.Diagnostics.Process? _browser;
    private ChromiumCdpSession? _session;

    public MobileScreenSurface(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths,
        int debuggingPort = DebugLayout.ProductScreensDebuggingPort)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
        _debuggingPort = debuggingPort;
    }

    /// <summary>
    /// The run starts on the connect screen and signs in by choosing Demo, which is the user
    /// flow and leaves the first screen in a state worth capturing. Nothing is seeded into
    /// storage, so the client is not driven down a path a person could not take.
    /// </summary>
    private static string MobileConnectUrl => $"{DebugLayout.MobileUrl}/connect";

    public string Surface => ProductSurface.Mobile;

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        var profile = _paths.EnsureUnderRoot(Path.Join(_paths.SessionDirectory, "chrome-product-screens"));
        Directory.CreateDirectory(profile);
        _browser = _processes.Start(ChromiumCommand(profile));
        _session = await ChromiumCdpSession.ConnectAsync(await WaitForDebuggerAsync(cancellationToken), cancellationToken);
        await SendAsync(id => ChromiumCdpMessageProvider.MobileDeviceMetrics(DebugLayout.MobileScreenWidth, DebugLayout.MobileScreenHeight, id), cancellationToken);
        await SendAsync(id => ChromiumCdpMessageProvider.Navigate(MobileConnectUrl, id), cancellationToken);
        await Task.Delay(4000, cancellationToken);
    }

    public async Task RunAsync(IReadOnlyList<ScreenStepDto> steps, CancellationToken cancellationToken)
    {
        foreach (var step in steps)
            await StepAsync(step, cancellationToken);
    }

    public async Task CaptureAsync(string outputPath, CancellationToken cancellationToken)
    {
        var reply = await SendAsync(id => ChromiumCdpMessageProvider.CaptureScreenshot(id), cancellationToken);
        await File.WriteAllBytesAsync(outputPath, ChromiumCdpMessageProvider.ReadPng(reply, "Mobile screen capture"), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
            await _session.DisposeAsync();
        if (_browser is not null)
        {
            _processes.KillTree(_browser.Id);
            _browser.Dispose();
        }
    }

    private async Task StepAsync(ScreenStepDto step, CancellationToken cancellationToken)
    {
        switch (step.Kind)
        {
            case ScreenStepKind.Navigate:
                await EvaluateAsync(MobileScreenScriptProvider.Navigate(step.Target ?? "/"), $"navigate {step.Target}", cancellationToken);
                break;
            case ScreenStepKind.Tap:
                await TapAsync(MobileScreenScriptProvider.Locate(step.Target ?? ""), step.Target ?? "", cancellationToken);
                break;
            case ScreenStepKind.Fill:
                await TapAsync(MobileScreenScriptProvider.FocusField(step.Target ?? ""), step.Target ?? "", cancellationToken);
                await SendAsync(id => ChromiumCdpMessageProvider.InsertText(step.Text ?? "", id), cancellationToken);
                break;
            case ScreenStepKind.Settle:
                break;
            default:
                throw new InvalidOperationException($"Mobile screens cannot run a '{step.Kind}' step.");
        }

        await Task.Delay(step.DelayMs, cancellationToken);
    }

    private async Task TapAsync(string script, string label, CancellationToken cancellationToken)
    {
        var point = await WaitForPointAsync(script, label, cancellationToken);
        await SendAsync(id => ChromiumCdpMessageProvider.MouseEvent("mousePressed", point.X, point.Y, id), cancellationToken);
        await SendAsync(id => ChromiumCdpMessageProvider.MouseEvent("mouseReleased", point.X, point.Y, id), cancellationToken);
    }

    /// <summary>Polls rather than sleeping: a route must not pass because a fixed wait was generous.</summary>
    private async Task<(int X, int Y)> WaitForPointAsync(string script, string label, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var reply = await EvaluateAsync(script, $"locate '{label}'", cancellationToken);
            if (ChromiumCdpMessageProvider.ReadPoint(reply, $"locate '{label}'") is { } point)
                return point;
            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException($"Mobile screens timed out waiting for '{label}'.");
    }

    private Task<string> EvaluateAsync(string expression, string action, CancellationToken cancellationToken)
        => SendAsync(id => ChromiumCdpMessageProvider.Evaluate(expression, id), cancellationToken, action);

    private async Task<string> SendAsync(Func<int, string> message, CancellationToken cancellationToken, string? action = null)
    {
        var session = _session ?? throw new InvalidOperationException("Mobile screens were not opened.");
        var reply = await session.SendAsync(message, cancellationToken);
        if (action is not null)
            ChromiumCdpMessageProvider.ThrowIfEvaluateFailed(reply, action);
        return reply;
    }

    private AllowlistedCommand ChromiumCommand(string profile)
    {
        var arguments = new[]
        {
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--hide-scrollbars",
            $"--remote-debugging-port={_debuggingPort}",
            $"--user-data-dir={profile}",
            $"--window-size={DebugLayout.MobileScreenWidth},{DebugLayout.MobileScreenHeight}",
            MobileConnectUrl
        };
        var chromium = _environment.FindChromium();
        if (chromium is not null)
            return new AllowlistedCommand(Path.GetFileName(chromium), arguments, _paths.RepositoryRoot);

        var quoted = string.Join(' ', arguments.Select(BashQuote.Single));
        return new AllowlistedCommand("nix-shell", ["-p", "chromium", "--run", $"chromium {quoted}"], _paths.RepositoryRoot);
    }

    private async Task<string> WaitForDebuggerAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        while (!cancellationToken.IsCancellationRequested)
        {
            var websocketUrl = await TryReadDebuggerAsync(http, cancellationToken);
            if (websocketUrl is not null)
                return websocketUrl;
            await Task.Delay(250, cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private async Task<string?> TryReadDebuggerAsync(HttpClient http, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync($"http://127.0.0.1:{_debuggingPort}/json/list", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return ChromiumDebuggerListParser.ReadWebSocketUrl(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
