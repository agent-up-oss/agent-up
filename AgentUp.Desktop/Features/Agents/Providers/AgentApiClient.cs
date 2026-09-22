using System.Net.Http.Json;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;

namespace AgentUp.Desktop.Features.Agents.Providers;

public sealed class AgentApiClient : IAgentApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;
    private readonly HttpClient _events;

    public AgentApiClient(HttpClient http) : this(http, http)
    {
    }

    public AgentApiClient(HttpClient http, HttpClient eventsHttp)
    {
        _http = http;
        _events = eventsHttp;
    }

    private static string Root(string workspaceId) => $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/agent";

    public async Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken) { using var response = await _http.GetAsync(Root(workspaceId), cancellationToken); if (response.StatusCode == HttpStatusCode.NotFound) return null; await EnsureSuccessAsync(response, cancellationToken); return await response.Content.ReadFromJsonAsync<AgentSessionDto>(Options, cancellationToken); }
    public async Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken) { using var response = await _http.PostAsJsonAsync(Root(workspaceId), new { agent }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); return await response.Content.ReadFromJsonAsync<AgentSessionDto>(Options, cancellationToken); }
    public async Task<AgentSessionDto?> ResumeAsync(string workspaceId, string sessionId, CancellationToken cancellationToken) { using var response = await _http.PostAsJsonAsync(Root(workspaceId) + $"/sessions/{Uri.EscapeDataString(sessionId)}/resume", new { }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); return await response.Content.ReadFromJsonAsync<AgentSessionDto>(Options, cancellationToken); }
    public async Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken) { using var response = await _http.PostAsJsonAsync(Root(workspaceId) + "/messages", new { message }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }
    public async Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken) { using var response = await _http.PostAsJsonAsync(Root(workspaceId) + "/authenticate", new { methodId }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }
    public async Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken) { using var response = await _http.PostAsJsonAsync(Root(workspaceId) + "/permissions", new { requestId, optionId }, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }
    public async Task StopAsync(string workspaceId, CancellationToken cancellationToken) { using var response = await _http.DeleteAsync(Root(workspaceId), cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }

    public async IAsyncEnumerable<AgentEventDto> EventsAsync(string workspaceId, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _events.DefaultRequestHeaders.Authorization = _http.DefaultRequestHeaders.Authorization;
        using var request = new HttpRequestMessage(HttpMethod.Get, Root(workspaceId) + $"/events?after={after}");
        request.Headers.Accept.ParseAdd("text/event-stream");
        using var response = await _events.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            AgentEventDto? item;
            try
            {
                item = JsonSerializer.Deserialize<AgentEventDto>(line[6..], Options);
            }
            catch (JsonException)
            {
                continue;
            }

            if (item is not null)
                yield return item;
        }
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
