using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// The tool surface of an in-process agent. It is the Agent-Up MCP toolset narrowed three ways,
/// each only removing: to tools that name a workspace (so the call can be pinned), to tools the
/// team member's grant allows (through the same operation-permission map tokens use), and to
/// that one workspace. Two host tools sit beside them: <c>submit_review</c>, which raises review
/// threads only a reviewer may raise, and <c>complete_task</c>, which ends the loop task.
/// </summary>
public sealed class InProcessAgentToolService(
    ILoopbackMcpClient mcp,
    InProcessToolCatalogProvider catalog,
    TeamMemberService members,
    McpToolPermissionController permissions,
    IRoleProgress progress,
    ForgeController? forge = null) : IInProcessToolHost
{
    public const string CompleteTask = "complete_task";
    public const string SubmitReview = "submit_review";

    /// <summary>The MCP endpoints an in-process agent draws tools from. Code endpoints are absent on purpose.</summary>
    public static IReadOnlyList<string> Endpoints { get; } = ["/mcp/browser", "/mcp/orchestration"];

    private static readonly IReadOnlySet<string> ReviewEvents =
        new HashSet<string>(StringComparer.Ordinal) { "COMMENT", "REQUEST_CHANGES", "APPROVE" };

    public async Task<IReadOnlyList<InProcessTool>> ListAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var granted = members.Grants(workspaceId);
        var tools = new List<InProcessTool>();
        foreach (var endpoint in Endpoints)
        {
            foreach (var descriptor in await mcp.ListToolsAsync(endpoint, cancellationToken))
            {
                var tool = catalog.Describe(descriptor);
                if (tool is not null && Allowed(granted, tool.Name))
                    tools.Add(tool);
            }
        }

        tools.Add(CompleteTaskTool);
        if (granted.SubmitsReviews && forge is not null)
            tools.Add(SubmitReviewTool);
        return tools;
    }

    public async Task<InProcessToolResult> CallAsync(
        string workspaceId,
        string tool,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (tool == CompleteTask)
            return new InProcessToolResult("Task marked complete.", [], false, CompletesTask: true);
        if (tool == SubmitReview)
            return await SubmitReviewAsync(workspaceId, arguments, cancellationToken);

        var granted = members.Grants(workspaceId);
        if (!Allowed(granted, tool))
            return Refused($"Tool '{tool}' is outside what a {granted.Kind} team member may use.");
        var endpoint = await EndpointOfAsync(tool, cancellationToken);
        if (endpoint is null)
            return Refused($"Tool '{tool}' is not offered to in-process agents.");
        var result = await mcp.CallToolAsync(endpoint, tool, catalog.Pin(arguments, workspaceId), cancellationToken);
        return catalog.Read(result);
    }

    private bool Allowed(TeamMemberGrants granted, string tool)
    {
        var required = permissions.RequiredPermission(tool);
        return required is not null && granted.OperationPermissions.Contains(required);
    }

    private async Task<string?> EndpointOfAsync(string tool, CancellationToken cancellationToken)
    {
        foreach (var endpoint in Endpoints)
        {
            var offered = await mcp.ListToolsAsync(endpoint, cancellationToken);
            if (offered.Select(catalog.Describe).Any(item => item?.Name == tool))
                return endpoint;
        }

        return null;
    }

    private async Task<InProcessToolResult> SubmitReviewAsync(
        string workspaceId,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var refusal = members.ReviewRefusal(workspaceId);
        if (refusal is not null)
            return Refused(refusal);
        if (forge is null)
            return Refused("No forge is available on this Server.");
        var proposal = Text(arguments, "proposalNumber");
        var body = Text(arguments, "body");
        var reviewEvent = (Text(arguments, "event") ?? "COMMENT").ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(proposal) || string.IsNullOrWhiteSpace(body))
            return Refused("submit_review needs proposalNumber and body.");
        if (!ReviewEvents.Contains(reviewEvent))
            return Refused("event must be COMMENT, REQUEST_CHANGES, or APPROVE.");

        var outcome = await forge.SubmitReviewAsync(workspaceId, new ForgeReviewRequest(proposal, body, reviewEvent), cancellationToken);
        if (outcome.Refusal is not null)
            return Refused(outcome.Refusal);
        if (!outcome.Submitted)
            return Refused("This workspace has no connected forge to submit the review to.");
        // A reviewer role terminates on ReviewSubmitted; this is the event that satisfies it.
        progress.Record(workspaceId, RoleTerminationKind.ReviewSubmitted);
        return new InProcessToolResult($"Review submitted on proposal {proposal} as {reviewEvent}.", [], false);
    }

    private static string? Text(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;

    private static InProcessToolResult Refused(string reason) => new(reason, [], true);

    private static readonly InProcessTool CompleteTaskTool = new(
        CompleteTask,
        "Call once the assigned task is finished. Ends this task in the Agent-Up loop.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { summary = new { type = "string", description = "One-paragraph outcome." } }
        }));

    private static readonly InProcessTool SubmitReviewTool = new(
        SubmitReview,
        "Submit a review on a proposal. COMMENT and REQUEST_CHANGES raise threads the author must address; describe what you observed in the running application, the steps to see it, and the expected behavior.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                proposalNumber = new { type = "string", description = "The proposal (pull request) number under review." },
                body = new { type = "string", description = "The review body." },
                @event = new { type = "string", @enum = new[] { "COMMENT", "REQUEST_CHANGES", "APPROVE" } }
            },
            required = new[] { "proposalNumber", "body" }
        }));
}
