using System.ComponentModel;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Browser.Controllers;

[McpServerToolType]
public sealed class BrowserMcpTools(BrowserMcpService browser)
{
    [McpServerTool(Name = "browser_navigate", Title = "Navigate Browser")]
    [Description("Navigate the workspace browser to a URL. Use the workspace id and allocated HTTP port returned by start_workspace. If navigation fails or times out, inspect the workspace console immediately through Orchestration MCP before retrying browser actions.")]
    public Task<McpToolResult> Navigate(
        [Description("Registered workspace ID.")] string workspaceId,
        [Description("The URL to navigate to.")] string url,
        CancellationToken cancellationToken) =>
        browser.NavigateAsync(workspaceId, url, cancellationToken);

    [McpServerTool(Name = "browser_inspect", Title = "Inspect Page")]
    [Description("Return structured accessibility data for the active page: title, URL, headings, and interactive elements (links, buttons, inputs). If inspection fails or times out, inspect the workspace console immediately through Orchestration MCP.")]
    public Task<McpToolResult> InspectPage(
        [Description("Registered workspace ID.")] string workspaceId,
        CancellationToken cancellationToken) =>
        browser.InspectPageAsync(workspaceId, cancellationToken);

    [McpServerTool(Name = "browser_click", Title = "Click Element")]
    [Description("Click an element in the workspace browser by CSS selector. If the click fails or times out, inspect the workspace console immediately through Orchestration MCP.")]
    public Task<McpToolResult> Click(
        [Description("Registered workspace ID.")] string workspaceId,
        [Description("CSS selector for the element to click, e.g. 'button#submit' or 'a[href=\"/login\"]'.")] string selector,
        CancellationToken cancellationToken) =>
        browser.ClickAsync(workspaceId, selector, cancellationToken);

    [McpServerTool(Name = "browser_fill", Title = "Fill Input")]
    [Description("Set the value of an input or textarea in the workspace browser by CSS selector. Dispatches input and change events so reactive frameworks update. If filling fails or times out, inspect the workspace console immediately through Orchestration MCP.")]
    public Task<McpToolResult> Fill(
        [Description("Registered workspace ID.")] string workspaceId,
        [Description("CSS selector for the input element, e.g. 'input[name=\"email\"]'.")] string selector,
        [Description("Value to set.")] string text,
        CancellationToken cancellationToken) =>
        browser.FillAsync(workspaceId, selector, text, cancellationToken);

    [McpServerTool(Name = "browser_press", Title = "Press Key")]
    [Description("Dispatch keyboard events for a key on the currently focused element. Use key names such as Enter, Tab, Escape, ArrowDown. If key input fails or times out, inspect the workspace console immediately through Orchestration MCP.")]
    public Task<McpToolResult> Press(
        [Description("Registered workspace ID.")] string workspaceId,
        [Description("Key name, e.g. 'Enter', 'Tab', 'Escape', 'ArrowDown'.")] string key,
        CancellationToken cancellationToken) =>
        browser.PressAsync(workspaceId, key, cancellationToken);

    [McpServerTool(Name = "browser_wait_for_selector", Title = "Wait for Selector")]
    [Description("Wait until a CSS selector matches an element on the page. If waiting fails or times out, inspect the workspace console immediately through Orchestration MCP before trying more browser actions.")]
    public Task<McpToolResult> WaitForSelector(
        [Description("Registered workspace ID.")] string workspaceId,
        [Description("CSS selector to wait for.")] string selector,
        [Description("Maximum wait time in milliseconds. Defaults to 10000.")] int timeoutMs = 10_000,
        CancellationToken cancellationToken = default) =>
        browser.WaitForSelectorAsync(workspaceId, selector, timeoutMs, cancellationToken);

    [McpServerTool(Name = "browser_wait_for_text", Title = "Wait for Text")]
    [Description("Wait until specific text appears in the page body. If waiting fails or times out, inspect the workspace console immediately through Orchestration MCP before trying more browser actions.")]
    public Task<McpToolResult> WaitForText(
        [Description("Registered workspace ID.")] string workspaceId,
        [Description("Text to wait for.")] string text,
        [Description("Maximum wait time in milliseconds. Defaults to 10000.")] int timeoutMs = 10_000,
        CancellationToken cancellationToken = default) =>
        browser.WaitForTextAsync(workspaceId, text, timeoutMs, cancellationToken);

    [McpServerTool(Name = "browser_wait_for_navigation", Title = "Wait for Navigation")]
    [Description("Wait for the browser to complete a page navigation (document.readyState reaches 'complete'). If waiting fails or times out, inspect the workspace console immediately through Orchestration MCP before trying more browser actions.")]
    public Task<McpToolResult> WaitForNavigation(
        [Description("Registered workspace ID.")] string workspaceId,
        [Description("Maximum wait time in milliseconds. Defaults to 10000.")] int timeoutMs = 10_000,
        CancellationToken cancellationToken = default) =>
        browser.WaitForNavigationAsync(workspaceId, timeoutMs, cancellationToken);

    [McpServerTool(Name = "browser_screenshot", Title = "Screenshot")]
    [Description("Capture the active workspace browser page as a bounded PNG image. Returns an MCP image content block plus a Server-managed audit artifact id for later lookup. If capture fails or times out, inspect the workspace console immediately through Orchestration MCP.")]
    public Task<CallToolResult> Screenshot(
        [Description("Registered workspace ID.")] string workspaceId,
        CancellationToken cancellationToken) =>
        browser.ScreenshotCallToolResultAsync(workspaceId, cancellationToken);
}
