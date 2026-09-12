using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Mobile.Providers;

public sealed class ChromiumMobileDriver : IMobileSurfaceDriver
{
    public const int DebuggingPort = 19222;

    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;

    public ChromiumMobileDriver(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
    }

    public async Task LoginAsync(string serverUrl, string password, CancellationToken cancellationToken)
    {
        var userData = _paths.EnsureUnderRoot(Path.Join(_paths.SessionDirectory, "chrome-mobile"));
        Directory.CreateDirectory(userData);
        var command = ChromiumCommand(userData);
        using var browser = _processes.Start(command);
        try
        {
            var websocketUrl = await WaitForDebuggerAsync(cancellationToken);
            await DriveLoginAsync(websocketUrl, serverUrl, password, cancellationToken);
        }
        finally
        {
            _processes.KillTree(browser.Id);
        }
    }

    private AllowlistedCommand ChromiumCommand(string userData)
    {
        var args = new[]
        {
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            $"--remote-debugging-port={DebuggingPort}",
            $"--user-data-dir={userData}",
            "--window-size=1280,800",
            $"{DebugLayout.MobileUrl}/connect"
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
                    var websocketUrl = ChromiumDebuggerListParser.ReadWebSocketUrl(json);
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

    private static async Task DriveLoginAsync(
        string websocketUrl,
        string serverUrl,
        string password,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(websocketUrl), cancellationToken);
        await EvaluateAsync(
            socket,
            MobileLoginScriptProvider.Build(serverUrl, password),
            cancellationToken);
    }

    private static async Task EvaluateAsync(ClientWebSocket socket, string expression, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["method"] = "Runtime.evaluate",
            ["params"] = new Dictionary<string, object?>
            {
                ["expression"] = expression,
                ["awaitPromise"] = true,
                ["returnByValue"] = true
            }
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, cancellationToken);
        var buffer = new byte[65536];
        var result = await socket.ReceiveAsync(buffer, cancellationToken);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException($"Mobile login CDP failed: {error}");
        if (document.RootElement.TryGetProperty("result", out var body)
            && body.TryGetProperty("exceptionDetails", out var details))
            throw new InvalidOperationException($"Mobile login failed: {details}");
    }
}
