using System.Text.Json;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.DTOs;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Protocol;

namespace AgentUp.Server.Features.DesktopApplications.Services;

public sealed class DesktopMcpService(DesktopApplicationsController desktopApplications, AuditController audit)
{
    public Task<McpToolResult> InspectAsync(string workspaceId, string application)
    {
        var session = desktopApplications.Get(workspaceId, application);
        return Task.FromResult(session is null
            ? new McpToolResult(false, "Desktop application session is not running.")
            : new McpToolResult(true, "Desktop application inspected.", new
            {
                session.SessionId,
                session.Generation,
                session.State,
                session.Width,
                session.Height,
                semanticElementsAvailable = false,
                coordinateSystem = "framebuffer"
            }));
    }

    public async Task<McpToolResult> ClickAsync(
        string workspaceId,
        string application,
        long generation,
        int x,
        int y,
        int button,
        CancellationToken cancellationToken)
    {
        var request = new DesktopPointerRequest(generation, x, y, button);
        await desktopApplications.PointerAsync(workspaceId, application, request, true, cancellationToken);
        await desktopApplications.PointerAsync(workspaceId, application, request, false, cancellationToken);
        return new McpToolResult(true, $"Clicked desktop coordinate ({x}, {y}).", new { generation, x, y, button });
    }

    public async Task<McpToolResult> PressAsync(
        string workspaceId,
        string application,
        long generation,
        string key,
        CancellationToken cancellationToken)
    {
        await desktopApplications.KeyAsync(workspaceId, application, new DesktopKeyRequest(generation, key, true), cancellationToken);
        await desktopApplications.KeyAsync(workspaceId, application, new DesktopKeyRequest(generation, key, false), cancellationToken);
        return new McpToolResult(true, $"Pressed desktop key '{key}'.", new { generation, key });
    }

    public async Task<McpToolResult> FillAsync(
        string workspaceId,
        string application,
        long generation,
        int x,
        int y,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Length > 500) return new McpToolResult(false, "Desktop text cannot exceed 500 characters.");
        var clicked = await ClickAsync(workspaceId, application, generation, x, y, 0, cancellationToken);
        if (!clicked.Succeeded) return clicked;
        foreach (var character in text)
        {
            var key = character == ' ' ? "space" : character.ToString();
            await desktopApplications.KeyAsync(workspaceId, application, new DesktopKeyRequest(generation, key, true), cancellationToken);
            await desktopApplications.KeyAsync(workspaceId, application, new DesktopKeyRequest(generation, key, false), cancellationToken);
        }
        return new McpToolResult(true, $"Filled desktop input at ({x}, {y}).", new { generation, x, y, length = text.Length });
    }

    public async Task<CallToolResult> ScreenshotAsync(string workspaceId, string application, CancellationToken cancellationToken)
    {
        var session = desktopApplications.Get(workspaceId, application);
        if (session is null) return Error("Desktop application session is not running.");
        var image = await desktopApplications.CaptureAsync(workspaceId, application, session.Generation, cancellationToken);
        var imageBase64 = Convert.ToBase64String(image);
        var recorded = await audit.RecordScreenshotAsync(new AuditScreenshot(
            workspaceId,
            $"desktop://{Uri.EscapeDataString(application)}",
            "image/png",
            imageBase64,
            session.Width,
            session.Height), cancellationToken);
        return new CallToolResult
        {
            Content =
            [
                ImageContentBlock.FromBytes(image, "image/png"),
                new TextContentBlock { Text = JsonSerializer.Serialize(new
                {
                    session.Generation,
                    recorded.Event.EventId,
                    recorded.Artifact.ArtifactId,
                    session.Width,
                    session.Height
                }) }
            ]
        };
    }

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }]
    };
}
