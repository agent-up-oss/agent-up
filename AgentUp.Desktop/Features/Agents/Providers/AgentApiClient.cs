using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;

namespace AgentUp.Desktop.Features.Agents.Providers;

public sealed class AgentApiClient(HttpClient http) : IAgentApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static string Root(string workspaceId) => $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/agent";

    public Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken) => http.GetFromJsonAsync<AgentSessionDto>(Root(workspaceId), Options, cancellationToken);
    public async Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken) { using var response = await http.PostAsJsonAsync(Root(workspaceId), new { agent }, cancellationToken); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<AgentSessionDto>(Options, cancellationToken); }
    public async Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken) { using var response = await http.PostAsJsonAsync(Root(workspaceId) + "/messages", new { message }, cancellationToken); response.EnsureSuccessStatusCode(); }
    public async Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken) { using var response = await http.PostAsJsonAsync(Root(workspaceId) + "/permissions", new { requestId, optionId }, cancellationToken); response.EnsureSuccessStatusCode(); }
    public async Task StopAsync(string workspaceId, CancellationToken cancellationToken) { using var response = await http.DeleteAsync(Root(workspaceId), cancellationToken); response.EnsureSuccessStatusCode(); }

    public async IAsyncEnumerable<AgentEventDto> EventsAsync(string workspaceId, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Root(workspaceId) + $"/events?after={after}");
        request.Headers.Accept.ParseAdd("text/event-stream");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
            if (line.StartsWith("data: ", StringComparison.Ordinal) && JsonSerializer.Deserialize<AgentEventDto>(line[6..], Options) is { } item) yield return item;
    }
}
