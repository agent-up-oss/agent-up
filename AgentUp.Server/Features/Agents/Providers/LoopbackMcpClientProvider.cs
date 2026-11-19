using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Streamable-HTTP MCP to this Server's own listener. Loopback is the MCP authentication, so an
/// in-process agent reaches the same tools a local coding agent does, and every call it makes is
/// recorded by the same MCP audit path. Narrowing and workspace pinning happen in the caller.
/// </summary>
public sealed class LoopbackMcpClientProvider(IHttpClientFactory clients, IConfiguration configuration) : ILoopbackMcpClient
{
    public const string HttpClientName = "in-process-agent-mcp";
    public const string UrlSetting = "AGENTUP_INPROCESS_MCP_URL";

    private readonly ConcurrentDictionary<string, string> _sessions = new(StringComparer.Ordinal);
    private long _nextId;

    public async Task<IReadOnlyList<JsonElement>> ListToolsAsync(string endpoint, CancellationToken cancellationToken)
    {
        var result = await SendAsync(endpoint, "tools/list", new { }, cancellationToken);
        return result.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array
            ? [.. tools.EnumerateArray().Select(tool => tool.Clone())]
            : [];
    }

    public Task<JsonElement> CallToolAsync(
        string endpoint,
        string tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken) =>
        SendAsync(endpoint, "tools/call", new { name = tool, arguments }, cancellationToken);

    public Uri BaseUrl()
    {
        var configured = new[]
        {
            configuration[UrlSetting],
            configuration["urls"],
            configuration["ASPNETCORE_URLS"]
        }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "http://127.0.0.1:5000";
        var candidates = configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chosen = candidates.FirstOrDefault(url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                     ?? candidates.FirstOrDefault()
                     ?? "http://127.0.0.1:5000";
        return new Uri(LoopbackHost(chosen).TrimEnd('/') + "/");
    }

    /// <summary>A wildcard listen address is not something to dial; loopback is the same listener.</summary>
    public static string LoopbackHost(string url) => url
        .Replace("://*", "://127.0.0.1", StringComparison.Ordinal)
        .Replace("://+", "://127.0.0.1", StringComparison.Ordinal)
        .Replace("://0.0.0.0", "://127.0.0.1", StringComparison.Ordinal)
        .Replace("://[::]", "://127.0.0.1", StringComparison.Ordinal)
        .Replace("://localhost", "://127.0.0.1", StringComparison.OrdinalIgnoreCase);

    private async Task<JsonElement> SendAsync(string endpoint, string method, object parameters, CancellationToken cancellationToken)
    {
        var session = await SessionAsync(endpoint, cancellationToken);
        var payload = new { jsonrpc = "2.0", id = Interlocked.Increment(ref _nextId), method, @params = parameters };
        var (status, body, _) = await PostAsync(endpoint, payload, session, cancellationToken);
        if (status == 404 && session is not null)
        {
            // The Server forgets idle MCP sessions; open a new one once rather than failing the turn.
            _sessions.TryRemove(endpoint, out _);
            session = await SessionAsync(endpoint, cancellationToken);
            (status, body, _) = await PostAsync(endpoint, payload, session, cancellationToken);
        }

        if (status is < 200 or > 299)
            throw new InvalidOperationException($"Agent-Up MCP {endpoint} answered {status} to {method}.");
        var message = Parse(body);
        if (message.TryGetProperty("error", out var error))
            throw new InvalidOperationException($"Agent-Up MCP {endpoint} refused {method}: {error}");
        return message.TryGetProperty("result", out var result) ? result.Clone() : JsonSerializer.SerializeToElement(new { });
    }

    private async Task<string?> SessionAsync(string endpoint, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(endpoint, out var existing))
            return existing;
        var (status, _, session) = await PostAsync(endpoint, new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref _nextId),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "agent-up-in-process-agent", version = "1" }
            }
        }, null, cancellationToken);
        if (status is < 200 or > 299)
            throw new InvalidOperationException($"Agent-Up MCP {endpoint} answered {status} to initialize.");
        await PostAsync(endpoint, new { jsonrpc = "2.0", method = "notifications/initialized" }, session, cancellationToken);
        if (session is not null)
            _sessions[endpoint] = session;
        return session;
    }

    private async Task<(int Status, string Body, string? Session)> PostAsync(
        string endpoint,
        object payload,
        string? session,
        CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUrl(), endpoint.TrimStart('/')))
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (session is not null)
            request.Headers.Add("Mcp-Session-Id", session);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var returned = response.Headers.TryGetValues("Mcp-Session-Id", out var values) ? values.FirstOrDefault() : null;
            return ((int)response.StatusCode, body, returned ?? session);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"Agent-Up MCP {endpoint} is unreachable on loopback: {exception.Message}", exception);
        }
    }

    /// <summary>A streamable-HTTP answer is either plain JSON or one SSE <c>data:</c> frame.</summary>
    public static JsonElement Parse(string body)
    {
        var json = body.Trim();
        if (json.StartsWith("event:", StringComparison.Ordinal) || json.Contains("\ndata:", StringComparison.Ordinal)
            || json.StartsWith("data:", StringComparison.Ordinal))
        {
            var data = json.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .LastOrDefault(line => line.StartsWith("data:", StringComparison.Ordinal));
            json = data is null ? "{}" : data["data:".Length..].Trim();
        }

        try
        {
            using var document = JsonDocument.Parse(json.Length == 0 ? "{}" : json);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Agent-Up MCP returned a frame that is not JSON.", exception);
        }
    }
}
