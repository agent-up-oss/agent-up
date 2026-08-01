using System.Diagnostics;
using System.Text.Json;
using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Shared.Interfaces;
using System.Threading.Channels;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserMcpService(BrowserSessionStore store, AuditController audit)
{
    private static readonly TimeSpan DispatchGrace = TimeSpan.FromSeconds(5);
    private const int MaxInlineScreenshotBytes = 220 * 1024;

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
            r => ScreenshotResultAsync(workspaceId, r, ct));

    private async Task<McpToolResult> DispatchAsync(
        BrowserCommandDto command,
        CancellationToken ct,
        Func<BrowserCommandResultDto, McpToolResult> onSuccess)
        => await DispatchAsync(command, ct, result => Task.FromResult(onSuccess(result)));

    private async Task<McpToolResult> DispatchAsync(
        BrowserCommandDto command,
        CancellationToken ct,
        Func<BrowserCommandResultDto, Task<McpToolResult>> onSuccess)
    {
        try
        {
            var result = await store.DispatchAsync(command, DispatchTimeout(command), ct);
            var mcpResult = result.Success
                ? await onSuccess(result)
                : new McpToolResult(false, result.Error ?? "Browser command failed.");
            await RecordBrowserEventAsync(command, mcpResult.Succeeded ? "success" : "failure", result, mcpResult, ct);
            return mcpResult;
        }
        catch (Exception ex) when (ex is ChannelClosedException or InvalidOperationException)
        {
            var failed = new McpToolResult(false, "Browser command failed.");
            await RecordBrowserEventAsync(command, "failure", null, failed, ct);
            return failed;
        }
    }

    private async Task<McpToolResult> ScreenshotResultAsync(
        string workspaceId,
        BrowserCommandResultDto result,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.Data))
            return new McpToolResult(false, "Screenshot failed: desktop returned no image data.");

        var screenshot = JsonSerializer.Deserialize<BrowserScreenshotResultDto>(
            result.Data,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (screenshot is null)
            return new McpToolResult(false, "Screenshot failed: desktop returned invalid image data.");
        if (DecodedBase64Length(screenshot.ImageBase64) > MaxInlineScreenshotBytes)
            return new McpToolResult(
                false,
                $"Screenshot failed: inline image was larger than {MaxInlineScreenshotBytes} bytes.");

        var recorded = await audit.RecordScreenshotAsync(
            new AuditScreenshot(
                workspaceId,
                screenshot.Url,
                screenshot.MimeType,
                screenshot.ImageBase64,
                screenshot.Width,
                screenshot.Height),
            ct);

        return new McpToolResult(
            true,
            $"Screenshot captured and stored as audit artifact '{recorded.Artifact.ArtifactId}'.",
            new BrowserScreenshotMcpResultDto(
                recorded.Event.EventId,
                recorded.Artifact.ArtifactId,
                screenshot.Url,
                screenshot.MimeType,
                screenshot.Width,
                screenshot.Height,
                recorded.Artifact.SizeBytes,
                screenshot.ImageBase64));
    }

    private async Task RecordBrowserEventAsync(
        BrowserCommandDto command,
        string outcome,
        BrowserCommandResultDto? browserResult,
        McpToolResult mcpResult,
        CancellationToken ct)
    {
        if (command.Kind == BrowserCommandKind.Screenshot && mcpResult.Succeeded)
            return;

        var details = new Dictionary<string, string>
        {
            ["commandId"] = command.CommandId.ToString(),
            ["kind"] = command.Kind.ToString(),
            ["message"] = mcpResult.Message
        };
        Add(details, "url", command.Url);
        Add(details, "selector", command.Selector);
        Add(details, "text", command.Text);
        Add(details, "key", command.Key);
        if (browserResult?.Data is not null && command.Kind != BrowserCommandKind.Screenshot)
            AddPageStateDetails(details, browserResult.Data);
        if (browserResult?.Error is not null)
            details["error"] = browserResult.Error;

        await audit.RecordAsync(
            new AuditRecordRequest(
                "browser",
                "mcp",
                ToolName(command.Kind),
                outcome,
                command.WorkspaceId,
                details),
            ct);
    }

    private static void Add(IDictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            details[key] = value;
    }

    private static void AddPageStateDetails(IDictionary<string, string> details, string data)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;
            Add(details, "pageTitle", ReadString(root, "title"));
            Add(details, "pageUrl", ReadString(root, "url"));
            if (root.TryGetProperty("headings", out var headings) && headings.ValueKind == JsonValueKind.Array)
                Add(details, "headings", SummarizeHeadings(headings));
            if (root.TryGetProperty("interactive", out var interactive) && interactive.ValueKind == JsonValueKind.Array)
                details["interactiveCount"] = interactive.GetArrayLength().ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }
        catch (JsonException)
        {
            Trace.TraceInformation("[BrowserMcpService] Browser page state was not JSON; storing preview only.");
        }

        details["dataPreview"] = data;
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? SummarizeHeadings(JsonElement headings)
    {
        var values = headings
            .EnumerateArray()
            .Select(heading =>
            {
                var level = ReadString(heading, "level");
                var text = ReadString(heading, "text");
                return string.IsNullOrWhiteSpace(text) ? null : $"{level}: {text}";
            })
            .OfType<string>()
            .Take(8)
            .ToArray();
        return values.Length == 0 ? null : string.Join(" | ", values);
    }

    private static int DecodedBase64Length(string value)
    {
        var length = value.Trim().Length;
        if (length == 0)
            return 0;

        var padding = value.EndsWith("==", StringComparison.Ordinal) ? 2 :
            value.EndsWith("=", StringComparison.Ordinal) ? 1 : 0;
        return (length * 3 / 4) - padding;
    }

    private static string ToolName(BrowserCommandKind kind) => kind switch
    {
        BrowserCommandKind.Navigate => "browser_navigate",
        BrowserCommandKind.InspectPage => "browser_inspect",
        BrowserCommandKind.Click => "browser_click",
        BrowserCommandKind.Fill => "browser_fill",
        BrowserCommandKind.Press => "browser_press",
        BrowserCommandKind.WaitForSelector => "browser_wait_for_selector",
        BrowserCommandKind.WaitForText => "browser_wait_for_text",
        BrowserCommandKind.WaitForNavigation => "browser_wait_for_navigation",
        BrowserCommandKind.Screenshot => "browser_screenshot",
        _ => $"{kind}"
    };

    private static TimeSpan DispatchTimeout(BrowserCommandDto command) =>
        TimeSpan.FromMilliseconds(Math.Max(0, command.TimeoutMs)) + DispatchGrace;
}
