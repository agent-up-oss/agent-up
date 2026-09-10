using System.Net.Http.Json;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;

namespace AgentUp.Desktop.Features.Agents.Providers;

public sealed class AgentApiClient(HttpClient http) : IAgentApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static string Root(string workspaceId) => $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/agent";

    public async Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken) { using var response = await http.GetAsync(Root(workspaceId), cancellationToken); if (response.StatusCode == HttpStatusCode.NotFound) return null; await EnsureSuccessAsync(response, cancellationToken); return await response.Content.ReadFromJsonAsync<AgentSessionDto>(Options, cancellationToken); }
    public async Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken) { using var response = await http.PostAsJsonAsync(Root(workspaceId), new { agent }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); return await response.Content.ReadFromJsonAsync<AgentSessionDto>(Options, cancellationToken); }
    public async Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken) { using var response = await http.PostAsJsonAsync(Root(workspaceId) + "/messages", new { message }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }
    public async Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken) { using var response = await http.PostAsJsonAsync(Root(workspaceId) + "/authenticate", new { methodId }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }
    public async Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken) { using var response = await http.PostAsJsonAsync(Root(workspaceId) + "/permissions", new { requestId, optionId }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }
    public async Task StopAsync(string workspaceId, CancellationToken cancellationToken) { using var response = await http.DeleteAsync(Root(workspaceId), cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }

    public async IAsyncEnumerable<AgentEventDto> EventsAsync(string workspaceId, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Root(workspaceId) + $"/events?after={after}");
        request.Headers.Accept.ParseAdd("text/event-stream");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
            if (line.StartsWith("data: ", StringComparison.Ordinal) && JsonSerializer.Deserialize<AgentEventDto>(line[6..], Options) is { } item) yield return item;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = TryReadProblemDetail(body) ?? $"The Server returned HTTP {(int)response.StatusCode}.";
        throw new HttpRequestException(detail, null, response.StatusCode);
    }

    private static string? TryReadProblemDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
