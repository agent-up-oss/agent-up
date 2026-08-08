using System.Collections.Concurrent;
using System.Text.Json;
using AgentUp.Server.Features.Browser.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserInputDispatcher(
    HeadlessBrowserSessionAccessor accessor,
    HeadlessBrowserSessionManager manager,
    BrowserRemoteDisplayService display,
    ILogger<BrowserInputDispatcher> logger)
{
    private readonly ConcurrentDictionary<string, string> _lastCursorByWorkspace = new();

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
                display.RegisterInputActivity(workspaceId);
            await (type switch
            {
                "mousemove"   => MoveMouseAsync(workspaceId, session, root, ct),
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
            logger.LogDebug(ex, "Input dispatch failed for workspace {WorkspaceId}.", Sanitize(workspaceId));
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

    private async Task MoveMouseAsync(string workspaceId, BrowserSessionState session, JsonElement root, CancellationToken ct)
    {
        var x = X(root);
        var y = Y(root);
        await session.Page.Mouse.MoveAsync(x, y).WaitAsync(ct);
        var cursor = await ReadCursorAsync(session.Page, x, y, ct);
        if (!_lastCursorByWorkspace.TryGetValue(workspaceId, out var last) ||
            !string.Equals(last, cursor, StringComparison.Ordinal))
        {
            _lastCursorByWorkspace[workspaceId] = cursor;
            await display.BroadcastTextAsync(workspaceId, JsonSerializer.Serialize(new
            {
                type = "cursor",
                cursor
            }), ct);
        }
    }

    private static async Task<string> ReadCursorAsync(IPage page, decimal x, decimal y, CancellationToken ct)
    {
        var script = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $$"""
            (() => {
              const el = document.elementFromPoint({{x}}, {{y}});
              if (!el) return 'default';
              const cursor = getComputedStyle(el).cursor || 'default';
              return cursor === 'auto' ? 'default' : cursor;
            })()
            """);
        var cursor = await page.EvaluateExpressionAsync<string>(script).WaitAsync(ct);
        return CursorKind(cursor);
    }

    private static string Sanitize(string id) =>
        id.Replace("\r", string.Empty, StringComparison.Ordinal)
          .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static string CursorKind(string? cursor)
        => cursor switch
        {
            "pointer" => "pointer",
            "text" or "vertical-text" => "text",
            "grab" or "grabbing" => "grab",
            _ => "default"
        };

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
