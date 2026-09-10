using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Interfaces;

namespace AgentUp.Desktop.Features.Git.Providers;

public sealed class GitApiClient(HttpClient http) : IGitApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<GitChangeTreeDto?> GetChangesAsync(string workspaceId, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/git/changes", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitChangeTreeDto>(Options, ct);
    }

    public async Task<GitFileDiffDto?> GetFileDiffAsync(string workspaceId, string path, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/git/file?path={Uri.EscapeDataString(path)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitFileDiffDto>(Options, ct);
    }

    public async Task<GitCommitResultDto> CommitAsync(
        string workspaceId,
        GitCommitRequestDto request,
        CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/git/commit", request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new GitCommitResultDto(false, false, null, "This workspace is no longer registered.");

        if (!response.IsSuccessStatusCode)
            return new GitCommitResultDto(true, false, null, await ReadProblemDetailAsync(response));

        var result = await response.Content.ReadFromJsonAsync<GitCommitResultDto>(Options, ct);
        return result ?? new GitCommitResultDto(true, false, null, "The server returned an empty commit result.");
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("detail", out var detail)
                ? detail.GetString() ?? body
                : body;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return $"HTTP {(int)response.StatusCode}";
        }
    }
}
