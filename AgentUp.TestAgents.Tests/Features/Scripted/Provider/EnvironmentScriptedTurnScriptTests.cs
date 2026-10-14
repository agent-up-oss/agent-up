using AgentUp.TestAgents.Features.Scripted.Models;
using AgentUp.TestAgents.Features.Scripted.Providers;

namespace AgentUp.TestAgents.Tests.Features.Scripted.Provider;

[TestFixture]
public sealed class EnvironmentScriptedTurnScriptTests
{
    [TearDown]
    public void ClearOverrides()
    {
        Environment.SetEnvironmentVariable("AGENTUP_SCRIPTED_SCRIPT", null);
        Environment.SetEnvironmentVariable("AGENTUP_SCRIPTED_TURNS", null);
    }

    [Test]
    public void Load_defaultsToACompletedTurn()
    {
        var script = new EnvironmentScriptedTurnScript().Load();

        Assert.That(script.Turns, Has.Count.EqualTo(1));
        Assert.That(script.Turns[0].Kind, Is.EqualTo(ScriptedTurnKind.Complete));
    }

    [Test]
    public void Load_readsInlineTurnsFromTheEnvironment()
    {
        Environment.SetEnvironmentVariable("AGENTUP_SCRIPTED_TURNS", """[{"kind":"rateLimited"},{"kind":"permission"}]""");

        var script = new EnvironmentScriptedTurnScript().Load();

        Assert.That(script.Turns.Select(turn => turn.Kind), Is.EqualTo([ScriptedTurnKind.RateLimited, ScriptedTurnKind.Permission]));
    }

    [Test]
    public void Load_prefersAScriptFileOverInlineTurns()
    {
        var file = Path.Join(Path.GetTempPath(), $"agent-up-scripted-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, """{"turns":[{"kind":"endTurn"}]}""");
        Environment.SetEnvironmentVariable("AGENTUP_SCRIPTED_SCRIPT", file);
        Environment.SetEnvironmentVariable("AGENTUP_SCRIPTED_TURNS", """[{"kind":"crash"}]""");

        try
        {
            var script = new EnvironmentScriptedTurnScript().Load();
            Assert.That(script.Turns[0].Kind, Is.EqualTo(ScriptedTurnKind.EndTurn));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
