using System.ComponentModel;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.DesktopApplications.Controllers;

[McpServerToolType]
public sealed class DesktopMcpTools(DesktopMcpService desktop)
{
    [McpServerTool(Name = "desktop_inspect", Title = "Inspect Desktop Application")]
    [Description("Inspect a running hosted desktop application and return its current session generation and framebuffer coordinate system.")]
    public Task<McpToolResult> Inspect(string workspaceId, string application) =>
        desktop.InspectAsync(workspaceId, application);

    [McpServerTool(Name = "desktop_click", Title = "Click Desktop Application")]
    [Description("Click a logical framebuffer coordinate from the latest desktop inspection or screenshot. Stale generations are rejected.")]
    public Task<McpToolResult> Click(string workspaceId, string application, long generation, int x, int y, int button = 0, CancellationToken cancellationToken = default) =>
        desktop.ClickAsync(workspaceId, application, generation, x, y, button, cancellationToken);

    [McpServerTool(Name = "desktop_press", Title = "Press Desktop Key")]
    [Description("Send one key press to a hosted desktop application. Stale session generations are rejected.")]
    public Task<McpToolResult> Press(string workspaceId, string application, long generation, string key, CancellationToken cancellationToken) =>
        desktop.PressAsync(workspaceId, application, generation, key, cancellationToken);

    [McpServerTool(Name = "desktop_fill", Title = "Fill Desktop Input")]
    [Description("Focus a desktop input at a logical framebuffer coordinate and type text. Stale session generations are rejected.")]
    public Task<McpToolResult> Fill(string workspaceId, string application, long generation, int x, int y, string text, CancellationToken cancellationToken) =>
        desktop.FillAsync(workspaceId, application, generation, x, y, text, cancellationToken);

    [McpServerTool(Name = "desktop_screenshot", Title = "Screenshot Desktop Application")]
    [Description("Capture the Server-owned framebuffer for a hosted desktop application and return an inline image plus audit artifact metadata.")]
    public Task<CallToolResult> Screenshot(string workspaceId, string application, CancellationToken cancellationToken) =>
        desktop.ScreenshotAsync(workspaceId, application, cancellationToken);
}
