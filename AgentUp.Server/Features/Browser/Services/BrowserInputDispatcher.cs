using System.Text.Json;
using AgentUp.Server.Features.Browser.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserInputDispatcher(
    HeadlessBrowserSessionAccessor accessor,
    HeadlessBrowserSessionManager manager,
    ScreencastBroadcastService broadcast,
    ILogger<BrowserInputDispatcher> logger)
{
    public async Task DispatchAsync(string workspaceId, string json, CancellationToken ct)
    {
        var session = accessor.GetSession(workspaceId);
        if (session is null) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type is not null)
                broadcast.RegisterInputActivity(workspaceId);
            await (type switch
            {
                "mousemove"   => session.Page.Mouse.MoveAsync(X(root), Y(root)).WaitAsync(ct),
                "mousedown"   => session.Page.Mouse.DownAsync(ClickOpts(root)).WaitAsync(ct),
                "mouseup"     => session.Page.Mouse.UpAsync(ClickOpts(root)).WaitAsync(ct),
                "click"       => session.Page.Mouse.ClickAsync(X(root), Y(root), ClickOpts(root)).WaitAsync(ct),
                "wheel"       => session.Page.Mouse.WheelAsync(Delta(root, "deltaX"), Delta(root, "deltaY")).WaitAsync(ct),
                "keydown"     => session.Page.Keyboard.DownAsync(Key(root)).WaitAsync(ct),
                "keyup"       => session.Page.Keyboard.UpAsync(Key(root)).WaitAsync(ct),
                "type"        => session.Page.Keyboard.TypeAsync(Text(root)).WaitAsync(ct),
                "controlmode" => SwitchToHumanAsync(workspaceId, root, ct),
                _             => Task.CompletedTask
            });
        }
        catch (Exception ex) when (ex is PuppeteerException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or OperationCanceledException)
        {
            logger.LogDebug(ex, "Input dispatch failed for workspace {WorkspaceId}.", workspaceId);
        }
    }

    private async Task SwitchToHumanAsync(string workspaceId, JsonElement root, CancellationToken ct)
    {
        var current = manager.GetControlMode(workspaceId);
        if (current.Authority != ControlAuthority.Human)
            await manager.SetControlModeAsync(workspaceId, BrowserControlMode.DefaultHuman, ct);
        if (root.TryGetProperty("width", out var w) && root.TryGetProperty("height", out var h))
            await manager.TrySetViewportAsync(workspaceId, w.GetInt32(), h.GetInt32(), ct);
    }

    private static decimal X(JsonElement e) => (decimal)e.GetProperty("x").GetDouble();
    private static decimal Y(JsonElement e) => (decimal)e.GetProperty("y").GetDouble();
    private static decimal Delta(JsonElement e, string prop) => (decimal)e.GetProperty(prop).GetDouble();
    private static string Key(JsonElement e) => e.GetProperty("key").GetString() ?? string.Empty;
    private static string Text(JsonElement e) => e.GetProperty("text").GetString() ?? string.Empty;

    private static ClickOptions ClickOpts(JsonElement e)
    {
        var button = e.TryGetProperty("button", out var b) ? b.GetString() : null;
        var count = e.TryGetProperty("clickCount", out var c) ? c.GetInt32() : 1;
        return new ClickOptions
        {
            Button = button switch { "middle" => MouseButton.Middle, "right" => MouseButton.Right, _ => MouseButton.Left },
            Count = count
        };
    }
}
