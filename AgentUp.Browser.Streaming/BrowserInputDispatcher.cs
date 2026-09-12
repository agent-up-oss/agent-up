using System.Text.Json;
using AgentUp.Browser.Streaming.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace AgentUp.Browser.Streaming;

public sealed class BrowserInputDispatcher(
    HeadlessBrowserSessionAccessor accessor,
    HeadlessBrowserSessionManager manager,
    BrowserRemoteDisplayService display,
    BrowserInputParser parser,
    CursorBroadcastTracker cursors,
    ILogger<BrowserInputDispatcher> logger)
{
    public async Task DispatchAsync(string workspaceId, string json, CancellationToken ct)
    {
        var session = accessor.GetSession(workspaceId);
        if (session is null) return;
        try
        {
            var command = parser.Parse(json);
            if (command.Type is not null)
                display.RegisterInputActivity(workspaceId);

            await (command.Kind switch
            {
                BrowserInputKind.MouseMove => MoveMouseAsync(workspaceId, session, command, ct),
                BrowserInputKind.MouseDown => session.Page.Mouse.DownAsync(command.ClickOptions).WaitAsync(ct),
                BrowserInputKind.MouseUp => session.Page.Mouse.UpAsync(command.ClickOptions).WaitAsync(ct),
                BrowserInputKind.Click => session.Page.Mouse
                    .ClickAsync(command.X, command.Y, command.ClickOptions).WaitAsync(ct),
                BrowserInputKind.Wheel => session.Page.Mouse
                    .WheelAsync(command.DeltaX, command.DeltaY).WaitAsync(ct),
                BrowserInputKind.KeyDown => session.Page.Keyboard.DownAsync(command.Key).WaitAsync(ct),
                BrowserInputKind.KeyUp => session.Page.Keyboard.UpAsync(command.Key).WaitAsync(ct),
                BrowserInputKind.Type => session.Page.Keyboard.TypeAsync(command.Text).WaitAsync(ct),
                BrowserInputKind.ControlMode => SwitchToHumanAsync(workspaceId, command, ct),
                _ => Task.CompletedTask
            });
        }
        catch (Exception ex) when (ex is PuppeteerException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or OperationCanceledException)
        {
            logger.LogDebug(ex, "Input dispatch failed for workspace {WorkspaceId}.", Sanitize(workspaceId));
        }
    }

    private async Task SwitchToHumanAsync(string workspaceId, BrowserInputCommand command, CancellationToken ct)
    {
        var current = manager.GetControlMode(workspaceId);
        if (current.Authority != ControlAuthority.Human)
            await manager.SetControlModeAsync(workspaceId, BrowserControlMode.DefaultHuman, ct);
        if (command.HasViewport)
            await manager.TrySetViewportAsync(workspaceId, command.Width!.Value, command.Height!.Value, ct);
    }

    private async Task MoveMouseAsync(string workspaceId, BrowserSessionState session, BrowserInputCommand command, CancellationToken ct)
    {
        var x = command.X;
        var y = command.Y;
        await session.Page.Mouse.MoveAsync(x, y).WaitAsync(ct);
        var cursor = await ReadCursorAsync(session.Page, x, y, ct);
        if (cursors.ShouldBroadcast(workspaceId, cursor))
        {
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
        return BrowserCursorKind.From(cursor);
    }

    private static string Sanitize(string id) =>
        id.Replace("\r", string.Empty, StringComparison.Ordinal)
          .Replace("\n", string.Empty, StringComparison.Ordinal);
}
