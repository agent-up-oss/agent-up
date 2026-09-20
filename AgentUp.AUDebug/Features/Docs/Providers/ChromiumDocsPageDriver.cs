using System.Net.WebSockets;
using System.Text;
using AgentUp.AUDebug.Features.Docs.Interfaces;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Docs.Providers;

public sealed class ChromiumDocsPageDriver : IDocsPageCapture
{
    public const int DebuggingPort = DebugLayout.DocsDebuggingPort;

    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;

    public ChromiumDocsPageDriver(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
    }

    public async Task CaptureAsync(
        string url,
        string outputPath,
        string? heading,
        bool fullPage,
        CancellationToken cancellationToken)
    {
        var destination = _paths.EnsureUnderRoot(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var userData = _paths.EnsureUnderRoot(Path.Join(_paths.SessionDirectory, "chrome-docs"));
        Directory.CreateDirectory(userData);
        using var browser = _processes.Start(ChromiumCommand(userData, url));
        try
        {
            var websocketUrl = await WaitForDebuggerAsync(cancellationToken);
            await DriveCaptureAsync(websocketUrl, url, destination, heading, fullPage, cancellationToken);
        }
        finally
        {
            _processes.KillTree(browser.Id);
        }
    }

    private AllowlistedCommand ChromiumCommand(string userData, string url)
    {
        var args = new[]
        {
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--hide-scrollbars",
            $"--remote-debugging-port={DebuggingPort}",
            $"--user-data-dir={userData}",
            $"--window-size={DebugLayout.DocsViewportWidth},{DebugLayout.DocsViewportHeight}",
            url
        };
        var chromium = _environment.FindOnPath("chromium")
                       ?? _environment.FindOnPath("chromium-browser")
                       ?? _environment.FindOnPath("google-chrome");
        if (chromium is not null)
            return new AllowlistedCommand(Path.GetFileName(chromium), args, _paths.RepositoryRoot);

        var quoted = string.Join(' ', args.Select(BashQuote.Single));
        return new AllowlistedCommand(
            "nix-shell",
            ["-p", "chromium", "--run", $"chromium {quoted}"],
            _paths.RepositoryRoot);
    }

    private static async Task<string> WaitForDebuggerAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var response = await http.GetAsync($"http://127.0.0.1:{DebuggingPort}/json/list", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var websocketUrl = ChromiumDebuggerListParser.ReadWebSocketUrl(json, "127.0.0.1:10100");
                    if (websocketUrl is not null)
                        return websocketUrl;
                }
            }
            catch (HttpRequestException)
            {
                await Task.Delay(200, cancellationToken);
                continue;
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static async Task DriveCaptureAsync(
        string websocketUrl,
        string url,
        string outputPath,
        string? heading,
        bool fullPage,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(websocketUrl), cancellationToken);
        var id = 1;
        await SendEvaluateAsync(socket, DocsPageReadyScriptProvider.Build(url), "Docs page", id++, cancellationToken);
        if (!string.IsNullOrWhiteSpace(heading))
            await SendEvaluateAsync(socket, DocsHeadingScriptProvider.Build(heading), "Docs heading", id++, cancellationToken);

        if (fullPage)
        {
            var sizeJson = await SendAsync(
                socket,
                ChromiumCdpMessageProvider.Evaluate(DocsPageSizeScriptProvider.Build(), id++),
                cancellationToken);
            var (width, height) = ChromiumCdpMessageProvider.ReadSize(sizeJson);
            height = Math.Clamp(height, DebugLayout.DocsViewportHeight, DebugLayout.DocsMaxCaptureHeight);
            width = Math.Max(width, DebugLayout.DocsViewportWidth);
            var metrics = await SendAsync(
                socket,
                ChromiumCdpMessageProvider.SetDeviceMetrics(width, height, id++),
                cancellationToken);
            ChromiumCdpMessageProvider.ThrowIfEvaluateFailed(metrics, "Docs viewport");
        }

        var screenshot = await SendAsync(
            socket,
            ChromiumCdpMessageProvider.CaptureScreenshot(id, fullPage),
            cancellationToken);
        await File.WriteAllBytesAsync(
            outputPath,
            ChromiumCdpMessageProvider.ReadPng(screenshot, "Docs screenshot"),
            cancellationToken);
    }

    private static async Task SendEvaluateAsync(
        ClientWebSocket socket,
        string expression,
        string action,
        int id,
        CancellationToken cancellationToken)
    {
        var json = await SendAsync(socket, ChromiumCdpMessageProvider.Evaluate(expression, id), cancellationToken);
        ChromiumCdpMessageProvider.ThrowIfEvaluateFailed(json, action);
    }

    private static async Task<string> SendAsync(
        ClientWebSocket socket,
        string message,
        CancellationToken cancellationToken)
    {
        await socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken);
        var buffer = new byte[65536];
        using var memory = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            memory.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }
}
