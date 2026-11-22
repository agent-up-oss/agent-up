using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Interfaces;

namespace AgentUp.Desktop.Features.Files.Providers;

public sealed class WorkspaceFilesApiClient(HttpClient http) : IWorkspaceFilesApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public Task<WorkspaceDirectoryListingDto?> GetDirectoryAsync(string workspaceId, string path, CancellationToken ct = default)
        => ReadAsync<WorkspaceDirectoryListingDto>($"{Base(workspaceId)}/directory?path={Uri.EscapeDataString(path)}", ct);

    public Task<WorkspaceFileContentDto?> GetFileAsync(string workspaceId, string path, CancellationToken ct = default)
        => ReadAsync<WorkspaceFileContentDto>($"{Base(workspaceId)}/file?path={Uri.EscapeDataString(path)}", ct);

    public async Task<WorkspaceFileSaveResultDto> SaveFileAsync(string workspaceId, WorkspaceFileWriteRequestDto request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"{Base(workspaceId)}/file", request, ct);
        if (response.IsSuccessStatusCode)
        {
            var saved = await response.Content.ReadFromJsonAsync<WorkspaceFileContentDto>(Options, ct);
            return saved is null ? WorkspaceFileSaveResultDto.Failed("The Server returned an empty save.") : WorkspaceFileSaveResultDto.Success(saved);
        }

        var (title, detail) = await ReadProblemAsync(response);
        var conflict = response.StatusCode == HttpStatusCode.Conflict
            && string.Equals(title, "File changed on the Server", StringComparison.Ordinal);
        return WorkspaceFileSaveResultDto.Failed(detail, conflict);
    }

    private async Task<T?> ReadAsync<T>(string url, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException((await ReadProblemAsync(response)).Detail);
        return await response.Content.ReadFromJsonAsync<T>(Options, ct);
    }

    private static string Base(string workspaceId) => $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/files";

    private static async Task<(string? Title, string Detail)> ReadProblemAsync(HttpResponseMessage response)
    {
        var fallback = $"HTTP {(int)response.StatusCode}";
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var title = root.TryGetProperty("title", out var titleNode) ? titleNode.GetString() : null;
            var detail = root.TryGetProperty("detail", out var detailNode) ? detailNode.GetString() : null;
            return (title, detail ?? title ?? fallback);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return (null, fallback);
        }
    }
}
