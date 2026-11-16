using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Providers;

public sealed class TaskSourceApiClient(HttpClient http) : ITaskSourceApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<TaskSource?> ReadAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/task-source", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskSource>(Options, cancellationToken);
    }

    public async Task<TaskSource?> SetAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            "api/task-source",
            new { moduleId, owner, repository },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskSource>(Options, cancellationToken);
    }

    public async Task<TaskSource?> ClearAsync(CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync("api/task-source", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskSource>(Options, cancellationToken);
    }

    public async Task<string?> StartLoginAsync(CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync("api/task-source/login", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var started = await response.Content.ReadFromJsonAsync<TaskSourceLogin>(Options, cancellationToken);
        return started?.AuthorizeUrl;
    }
}
