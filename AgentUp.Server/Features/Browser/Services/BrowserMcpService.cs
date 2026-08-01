using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Shared.Interfaces;
using System.Threading.Channels;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserMcpService(BrowserSessionStore store)
{
    private static readonly TimeSpan DispatchGrace = TimeSpan.FromSeconds(5);

    public Task<McpToolResult> NavigateAsync(string workspaceId, string url, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Navigate,
            Url: url, Selector: null, Text: null, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Navigated to {url}.", r.Data));

    public Task<McpToolResult> InspectPageAsync(string workspaceId, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.InspectPage,
            Url: null, Selector: null, Text: null, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, "Page inspected.", r.Data));

    public Task<McpToolResult> ClickAsync(string workspaceId, string selector, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Click,
            Url: null, Selector: selector, Text: null, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Clicked '{selector}'.", r.Data));

    public Task<McpToolResult> FillAsync(string workspaceId, string selector, string text, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Fill,
            Url: null, Selector: selector, Text: text, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Filled '{selector}'.", r.Data));

    public Task<McpToolResult> PressAsync(string workspaceId, string key, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Press,
            Url: null, Selector: null, Text: null, Key: key, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Pressed key '{key}'.", r.Data));

    public Task<McpToolResult> WaitForSelectorAsync(string workspaceId, string selector, int timeoutMs, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.WaitForSelector,
            Url: null, Selector: selector, Text: null, Key: null, TimeoutMs: timeoutMs), ct,
            r => new McpToolResult(true, $"Selector '{selector}' appeared.", r.Data));

    public Task<McpToolResult> WaitForTextAsync(string workspaceId, string text, int timeoutMs, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.WaitForText,
            Url: null, Selector: null, Text: text, Key: null, TimeoutMs: timeoutMs), ct,
            r => new McpToolResult(true, $"Text appeared.", r.Data));

    public Task<McpToolResult> WaitForNavigationAsync(string workspaceId, int timeoutMs, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.WaitForNavigation,
            Url: null, Selector: null, Text: null, Key: null, TimeoutMs: timeoutMs), ct,
            r => new McpToolResult(true, "Navigation complete.", r.Data));

    public Task<McpToolResult> ScreenshotAsync(string workspaceId, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Screenshot,
            Url: null, Selector: null, Text: null, Key: null, TimeoutMs: 20_000), ct,
            r => new McpToolResult(true, $"Screenshot saved to: {r.Data}", r.Data));

    private async Task<McpToolResult> DispatchAsync(
        BrowserCommandDto command,
        CancellationToken ct,
        Func<BrowserCommandResultDto, McpToolResult> onSuccess)
    {
        try
        {
            var result = await store.DispatchAsync(command, DispatchTimeout(command), ct);
            return result.Success
                ? onSuccess(result)
                : new McpToolResult(false, result.Error ?? "Browser command failed.");
        }
        catch (Exception ex) when (ex is ChannelClosedException or InvalidOperationException)
        {
            return new McpToolResult(false, "Browser command failed.");
        }
    }

    private static TimeSpan DispatchTimeout(BrowserCommandDto command) =>
        TimeSpan.FromMilliseconds(Math.Max(0, command.TimeoutMs)) + DispatchGrace;
}
