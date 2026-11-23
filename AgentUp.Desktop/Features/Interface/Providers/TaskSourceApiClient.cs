using System.Net;
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

    public async Task<TaskSource?> SaveAsync(string moduleId, string token, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync("api/task-source", new { moduleId, token }, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException(await Problem(response, cancellationToken));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskSource>(Options, cancellationToken);
    }

    public async Task<TaskSource?> ForgetAsync(string moduleId, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync($"api/task-source/{Uri.EscapeDataString(moduleId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskSource>(Options, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskSourceRepository>> ListRepositoriesAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/task-source/repositories", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TaskSourceRepository>>(Options, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<TaskSourcePull>> ListPullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var path = "api/task-source/pulls"
            + $"?moduleId={Uri.EscapeDataString(moduleId)}"
            + $"&owner={Uri.EscapeDataString(owner)}"
            + $"&repository={Uri.EscapeDataString(repository)}";
        using var response = await http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TaskSourcePull>>(Options, cancellationToken) ?? [];
    }

    private static async Task<string> Problem(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text)
                return text;
        }
        catch (JsonException)
        {
            return $"The Server refused the token ({(int)response.StatusCode}).";
        }

        return $"The Server refused the token ({(int)response.StatusCode}).";
    }
}
