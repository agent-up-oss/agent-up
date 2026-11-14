using System.Net.Http.Headers;
using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Reads open issues assigned to the authorized identity from GitHub. Tests inject a handler;
/// production uses this only when a host connection is authorized.
/// </summary>
public sealed class GitHubAssignedIssueProvider(HttpClient http) : IAssignedIssueQueue
{
    public async Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
        string workspaceId,
        AssignedIssueOrder order,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/issues?filter=assigned&state=open&per_page=100");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("agent-up", "1"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var issues = new List<AssignedIssue>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            issues.Add(new AssignedIssue(
                item.GetProperty("id").GetRawText(),
                item.GetProperty("number").GetInt32(),
                item.GetProperty("title").GetString() ?? "",
                item.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                item.GetProperty("created_at").GetDateTimeOffset(),
                item.GetProperty("updated_at").GetDateTimeOffset(),
                item.GetProperty("html_url").GetString() ?? ""));
        }

        IEnumerable<AssignedIssue> ordered = order switch
        {
            AssignedIssueOrder.CreatedAsc => issues.OrderBy(item => item.CreatedAt).ThenBy(item => item.Number),
            AssignedIssueOrder.UpdatedAsc => issues.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Number),
            _ => issues.OrderBy(item => item.Number)
        };
        return [.. ordered];
    }

    public void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues)
    {
    }

    /// <summary>Nothing contends for these issues, so the hold always succeeds.</summary>
    public Task<bool> TryHoldAsync(string workspaceId, string issueId, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <summary>Completion is recorded by the caller replacing the list.</summary>
    public Task CompleteAsync(string workspaceId, string issueId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
