using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;

namespace AgentUp.TestAgents.Features.Scripted.Providers;

/// <summary>
/// A minimal Streamable HTTP MCP client: initialize once per server, then <c>tools/call</c>. It
/// sends the headers the server was offered with, which is how a sandboxed agent's calls are
/// pinned to its own workspace.
/// </summary>
public sealed class HttpScriptedMcpClient(HttpClient http) : IScriptedMcpClient
{
    private const string SessionHeader = "Mcp-Session-Id";
    private readonly ConcurrentDictionary<string, string?> _sessions = new(StringComparer.Ordinal);
    private int _nextId;

    public async Task<ScriptedMcpResult> CallAsync(
        ScriptedMcpServer server,
        string tool,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_sessions.ContainsKey(server.Name))
                _sessions[server.Name] = await InitializeAsync(server, cancellationToken);
            var reply = await SendAsync(server, _sessions[server.Name], Request("tools/call", new JsonObject
            {
                ["name"] = tool,
                ["arguments"] = arguments.DeepClone()
            }), cancellationToken);
            return Result(reply.Body);
        }
        catch (HttpRequestException exception)
        {
            return new ScriptedMcpResult(true, exception.Message);
        }
    }

    private async Task<string?> InitializeAsync(ScriptedMcpServer server, CancellationToken cancellationToken)
    {
        var initialized = await SendAsync(server, null, Request("initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "test-agent-scripted", ["version"] = "1.0" }
        }), cancellationToken);
        await SendAsync(server, initialized.Session, new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/initialized"
        }, cancellationToken);
        return initialized.Session;
    }

    private async Task<(string? Session, JsonNode? Body)> SendAsync(
        ScriptedMcpServer server,
        string? session,
        JsonObject message,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, server.Url)
        {
            Content = new StringContent(message.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        foreach (var header in server.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (session is not null)
            request.Headers.TryAddWithoutValidation(SessionHeader, session);
        using var response = await http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{server.Name} answered HTTP {(int)response.StatusCode}. {text}".Trim());
        var returned = response.Headers.TryGetValues(SessionHeader, out var values) ? values.FirstOrDefault() : session;
        return (returned, Body(text));
    }

    /// <summary>The JSON-RPC message in a reply, whether it came as JSON or as one SSE event.</summary>
    internal static JsonNode? Body(string text)
    {
        var json = text.TrimStart().StartsWith('{')
            ? text
            : text.Split('\n')
                .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(line => line["data:".Length..].Trim())
                .LastOrDefault(line => line.StartsWith('{'));
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static ScriptedMcpResult Result(JsonNode? body)
    {
        if (body?["error"] is JsonObject error)
            return new ScriptedMcpResult(true, error["message"]?.GetValue<string>() ?? error.ToJsonString());
        var result = body?["result"];
        var text = string.Join("\n", (result?["content"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(item => item["text"]?.GetValue<string>() ?? string.Empty));
        var isError = result?["isError"] is JsonValue flag && flag.GetValue<bool>();
        return new ScriptedMcpResult(isError || result is null, text);
    }

    private JsonObject Request(string method, JsonObject parameters) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = Interlocked.Increment(ref _nextId),
        ["method"] = method,
        ["params"] = parameters
    };
}
