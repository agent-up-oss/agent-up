using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Acp.Services;

/// <summary>
/// A real ACP v1 agent over stdio: line-delimited JSON-RPC on stdin and stdout, the same wire the
/// Server speaks to codex, cursor, and claude.
/// <para>
/// It advertises exactly one subscription auth method and refuses <c>session/new</c> until a
/// credential exists on disk. That refusal is what drives the Server into
/// <c>authentication_required</c>, so the whole sign-in path downstream of it is real.
/// </para>
/// </summary>
public sealed class AcpAgentService(TestAgentSchema schema, ITestAgentCredentialStore credentials)
{
    private const int AuthRequired = -32000;

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonNode? frame;
            try
            {
                frame = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            if (frame is null)
                continue;

            var id = frame["id"];
            var method = frame["method"]?.GetValue<string>();
            if (method is null)
                continue;

            var response = Respond(method, id);
            if (response is null)
                continue;

            await output.WriteLineAsync(response.ToJsonString());
            await output.FlushAsync(cancellationToken);
        }
    }

    private JsonNode? Respond(string method, JsonNode? id) => method switch
    {
        "initialize" => Result(id, Initialize()),
        "session/new" => NewSession(id),
        "session/prompt" => Result(id, new JsonObject { ["stopReason"] = "end_turn" }),
        "session/cancel" => null,
        _ => Result(id, new JsonObject())
    };

    private JsonObject Initialize() => new()
    {
        ["protocolVersion"] = 1,
        ["agentCapabilities"] = new JsonObject { ["loadSession"] = false },
        ["authMethods"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = MethodId(),
                ["name"] = MethodName(),
                ["description"] = "Open the sign-in link and sign in with your subscription."
            }
        }
    };

    private JsonNode NewSession(JsonNode? id)
    {
        // No credential yet: refuse the way an unauthenticated agent does, which is what puts the
        // Server into authentication_required.
        if (credentials.Read() is null)
            return Error(id, AuthRequired, "Authentication required. Sign in with your subscription to continue.");

        return Result(id, new JsonObject { ["sessionId"] = $"test-session-{Guid.NewGuid():N}" });
    }

    /// <summary>
    /// Method ids mirror the real agents so the Server's subscription filtering sees the same
    /// shapes it sees in production.
    /// </summary>
    internal string MethodId() => schema switch
    {
        TestAgentSchema.LoopbackRedirect => "chatgpt",
        TestAgentSchema.DeviceCode => "chatgpt",
        TestAgentSchema.PastedCode => "claude-login",
        _ => "cursor_login"
    };

    internal string MethodName() => schema switch
    {
        TestAgentSchema.LoopbackRedirect or TestAgentSchema.DeviceCode => "ChatGPT",
        TestAgentSchema.PastedCode => "Claude Pro",
        _ => "Cursor Login"
    };

    private static JsonNode Result(JsonNode? id, JsonObject result) => new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result
    };

    private static JsonNode Error(JsonNode? id, int code, string message) => new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };
}
