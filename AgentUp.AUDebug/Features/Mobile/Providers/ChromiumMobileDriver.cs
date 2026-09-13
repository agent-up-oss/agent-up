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

    public string UserDataDirectory
        => _paths.EnsureUnderRoot(Path.Join(_paths.SessionDirectory, "chrome-mobile"));

    public static bool IsPageWorldDestroyed(string cdpError)
        => cdpError.Contains("Execution context was destroyed", StringComparison.Ordinal);

    public async Task LoginAsync(string serverUrl, string password, CancellationToken cancellationToken)
    {
        var userData = UserDataDirectory;
        Directory.CreateDirectory(userData);
        var command = ChromiumCommand(userData, $"{DebugLayout.MobileUrl}/connect");
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

    public async Task CaptureAgentAsync(string outputPath, CancellationToken cancellationToken)
    {
        var userData = UserDataDirectory;
        Directory.CreateDirectory(userData);
        var destination = _paths.EnsureUnderRoot(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var command = ChromiumCommand(userData, $"{DebugLayout.MobileUrl}/");
        using var browser = _processes.Start(command);
        try
        {
            var websocketUrl = await WaitForDebuggerAsync(cancellationToken);
            await DriveAgentAsync(websocketUrl, destination, cancellationToken);
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
            $"--remote-debugging-port={DebuggingPort}",
            $"--user-data-dir={userData}",
            "--window-size=1280,800",
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
        try
        {
            await EvaluateAsync(
                socket,
                MobileLoginScriptProvider.Build(serverUrl, password),
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsPageWorldDestroyed(ex.Message))
        {
            System.Diagnostics.Trace.WriteLine(ex.Message);
        }

        await Task.Delay(2000, cancellationToken);
    }

    private static async Task DriveAgentAsync(
        string websocketUrl,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(websocketUrl), cancellationToken);
        try
        {
            await EvaluateAsync(socket, MobileOpenAgentScriptProvider.Build(), cancellationToken, "Mobile open-agent");
            await Task.Delay(1500, cancellationToken);
            await CapturePageAsync(socket, outputPath, cancellationToken);
            return;
        }
        catch (InvalidOperationException ex) when (IsPageWorldDestroyed(ex.Message))
        {
            System.Diagnostics.Trace.WriteLine(ex.Message);
        }

        var next = await WaitForDebuggerAsync(cancellationToken);
        using var captured = new ClientWebSocket();
        await captured.ConnectAsync(new Uri(next), cancellationToken);
        await Task.Delay(1500, cancellationToken);
        await CapturePageAsync(captured, outputPath, cancellationToken);
    }

    private static async Task CapturePageAsync(
        ClientWebSocket socket,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = 2,
            ["method"] = "Page.captureScreenshot",
            ["params"] = new Dictionary<string, object?>
            {
                ["format"] = "png",
                ["fromSurface"] = true
            }
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, cancellationToken);
        var buffer = new byte[1_048_576];
        using var memory = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            memory.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                break;
        }

        using var document = JsonDocument.Parse(memory.ToArray());
        if (document.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException($"Mobile open-agent screenshot failed: {error}");
        if (!document.RootElement.TryGetProperty("result", out var body)
            || !body.TryGetProperty("data", out var data)
            || data.GetString() is not { Length: > 0 } png)
            throw new InvalidOperationException("Mobile open-agent screenshot did not return an image.");

        await File.WriteAllBytesAsync(outputPath, Convert.FromBase64String(png), cancellationToken);
    }

    private static async Task EvaluateAsync(
        ClientWebSocket socket,
        string expression,
        CancellationToken cancellationToken,
        string action = "Mobile login")
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
            throw new InvalidOperationException($"{action} CDP failed: {error}");
        if (document.RootElement.TryGetProperty("result", out var body)
            && body.TryGetProperty("exceptionDetails", out var details))
            throw new InvalidOperationException($"{action} failed: {details}");
    }
}
