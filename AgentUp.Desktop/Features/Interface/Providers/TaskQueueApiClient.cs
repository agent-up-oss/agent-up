using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Providers;

public sealed class TaskQueueApiClient(HttpClient http) : ITaskQueueApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<QueuedTask>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/tasks", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<QueuedTask>>(Options, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<OfferedIssue>> ListIssuesAsync(string workspaceId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/forge/issues",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<OfferedIssue>>(Options, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<OfferedIssue>> ListRepositoryIssuesAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var path = "api/task-source/issues"
            + $"?moduleId={Uri.EscapeDataString(moduleId)}"
            + $"&owner={Uri.EscapeDataString(owner)}"
            + $"&repository={Uri.EscapeDataString(repository)}";
        using var response = await http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<OfferedIssue>>(Options, cancellationToken) ?? [];
    }

    public async Task EnqueueAsync(
        string? workspaceId,
        string issueId,
        string? targetWorkspaceId,
        CancellationToken cancellationToken,
        string? moduleId = null,
        string? owner = null,
        string? repository = null)
    {
        var body = new Dictionary<string, string?>
        {
            ["workspaceId"] = workspaceId,
            ["issueId"] = issueId,
            ["targetWorkspaceId"] = targetWorkspaceId,
        };
        if (moduleId is not null)
            body["moduleId"] = moduleId;
        if (owner is not null)
            body["owner"] = owner;
        if (repository is not null)
            body["repository"] = repository;
        using var response = await http.PostAsJsonAsync("api/tasks", body, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveAsync(QueuedTask task, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(Root(task), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task MoveAsync(QueuedTask task, int index, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(Root(task) + "/position", new { index }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TargetAsync(QueuedTask task, string? workspaceId, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(Root(task) + "/target", new { workspaceId }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReleaseAsync(QueuedTask task, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Root(task) + "/release", new { }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string Root(QueuedTask task) =>
        $"api/tasks/{Uri.EscapeDataString(task.Owner)}/{Uri.EscapeDataString(task.Repository)}/{Uri.EscapeDataString(task.Id)}";
}
