using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;

namespace AgentUp.TestAgents.Features.Scripted.Providers;

public sealed class EnvironmentScriptedTurnScript : IScriptedTurnScript
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ScriptedAgentScript Load()
    {
        var file = Environment.GetEnvironmentVariable("AGENTUP_SCRIPTED_SCRIPT");
        if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
            return JsonSerializer.Deserialize<ScriptedAgentScript>(File.ReadAllText(file), Json) ?? ScriptedAgentScript.Empty;

        var inline = Environment.GetEnvironmentVariable("AGENTUP_SCRIPTED_TURNS");
        if (string.IsNullOrWhiteSpace(inline))
            return new ScriptedAgentScript([new ScriptedTurn(ScriptedTurnKind.Complete)]);

        var turns = JsonSerializer.Deserialize<IReadOnlyList<ScriptedTurn>>(inline, Json) ?? [];
        return new ScriptedAgentScript(turns);
    }
}
