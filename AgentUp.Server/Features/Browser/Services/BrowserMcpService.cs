using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Shared.Interfaces;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserMcpService(BrowserSessionStore store)
{
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(30);

    public Task<McpToolResult> NavigateAsync(string workspaceId, string url, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Navigate,
            Url: url, Selector: null, Text: null, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Navigated to {url}."));

    public Task<McpToolResult> InspectPageAsync(string workspaceId, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.InspectPage,
            Url: null, Selector: null, Text: null, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, "Page inspected.", r.Data));

    public Task<McpToolResult> ClickAsync(string workspaceId, string selector, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Click,
            Url: null, Selector: selector, Text: null, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Clicked '{selector}'."));

    public Task<McpToolResult> FillAsync(string workspaceId, string selector, string text, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Fill,
            Url: null, Selector: selector, Text: text, Key: null, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Filled '{selector}'."));

    public Task<McpToolResult> PressAsync(string workspaceId, string key, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Press,
            Url: null, Selector: null, Text: null, Key: key, TimeoutMs: 10_000), ct,
            r => new McpToolResult(true, $"Pressed key '{key}'."));

    public Task<McpToolResult> WaitForSelectorAsync(string workspaceId, string selector, int timeoutMs, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.WaitForSelector,
            Url: null, Selector: selector, Text: null, Key: null, TimeoutMs: timeoutMs), ct,
            r => new McpToolResult(true, $"Selector '{selector}' appeared."));

    public Task<McpToolResult> WaitForTextAsync(string workspaceId, string text, int timeoutMs, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.WaitForText,
            Url: null, Selector: null, Text: text, Key: null, TimeoutMs: timeoutMs), ct,
            r => new McpToolResult(true, $"Text appeared."));

    public Task<McpToolResult> WaitForNavigationAsync(string workspaceId, int timeoutMs, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.WaitForNavigation,
            Url: null, Selector: null, Text: null, Key: null, TimeoutMs: timeoutMs), ct,
            r => new McpToolResult(true, "Navigation complete."));

    public Task<McpToolResult> ScreenshotAsync(string workspaceId, CancellationToken ct) =>
        DispatchAsync(new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Screenshot,
            Url: null, Selector: null, Text: null, Key: null, TimeoutMs: 20_000), ct,
            r => new McpToolResult(true, $"Screenshot saved to: {r.Data}", r.Data));

    private async Task<McpToolResult> DispatchAsync(
        BrowserCommandDto command,
        CancellationToken ct,
        Func<BrowserCommandResultDto, McpToolResult> onSuccess)
    {
        var result = await store.DispatchAsync(command, DispatchTimeout, ct);
        return result.Success
            ? onSuccess(result)
            : new McpToolResult(false, result.Error ?? "Browser command failed.");
    }
}
