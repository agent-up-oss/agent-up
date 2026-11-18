using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Providers;

/// <summary>
/// Writes the OpenCode configuration that points it at an Ollama-compatible endpoint. OpenCode
/// reads it from <see cref="ConfigVariable"/>, so nothing is written into the workspace and a
/// repository's own <c>opencode.json</c> keeps its other settings.
/// </summary>
public sealed class OpencodeConfigProvider
{
    public const string ConfigVariable = "OPENCODE_CONFIG_CONTENT";

    /// <summary>
    /// The variable that carries the token. The configuration names it through OpenCode's
    /// <c>{env:...}</c> substitution instead of holding the value, so the token never appears in
    /// a configuration document a process listing or a log line might show.
    /// </summary>
    public const string TokenVariable = "AGENT_UP_OPENCODE_API_KEY";

    public const string ProviderId = "agent-up";

    public string Write(string endpoint, string model, bool hasToken)
    {
        var options = new JsonObject { ["baseURL"] = BaseUrl(endpoint) };
        if (hasToken)
            options["apiKey"] = "{env:" + TokenVariable + "}";

        var config = new JsonObject
        {
            ["$schema"] = "https://opencode.ai/config.json",
            ["provider"] = new JsonObject
            {
                [ProviderId] = new JsonObject
                {
                    ["npm"] = "@ai-sdk/openai-compatible",
                    ["name"] = "Agent-Up",
                    ["options"] = options,
                    ["models"] = new JsonObject { [model] = new JsonObject { ["name"] = model } }
                }
            },
            ["model"] = ProviderId + "/" + model,
            // Ask rather than allow, so every edit and command reaches the Server as an ACP
            // permission request and a workspace permission policy can govern it.
            ["permission"] = new JsonObject
            {
                ["edit"] = "ask",
                ["bash"] = "ask",
                ["webfetch"] = "ask"
            }
        };
        return config.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// The OpenAI-compatible base URL. Ollama serves it under <c>/v1</c>, so an endpoint given as
    /// a bare host gains that path; an endpoint that already names a path is used as written.
    /// </summary>
    public static string BaseUrl(string endpoint)
    {
        var trimmed = endpoint.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return trimmed;
        return uri.AbsolutePath is "" or "/" ? trimmed + "/v1" : trimmed;
    }
}
