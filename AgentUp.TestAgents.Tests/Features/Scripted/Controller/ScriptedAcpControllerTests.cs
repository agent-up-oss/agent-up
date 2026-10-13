using AgentUp.TestAgents.Features.Scripted.Controllers;
using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;
using AgentUp.TestAgents.Features.Scripted.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Scripted.Controller;

[TestFixture]
public sealed class ScriptedAcpControllerTests
{
    [Test]
    public async Task ServeAsync_playsTheBoundScriptOverStdio()
    {
        var controller = new ScriptedAcpController(new ScriptedAcpService(new FixedScript(ScriptedTurnKind.Complete)));
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n"
            + """{"jsonrpc":"2.0","id":2,"method":"session/prompt","params":{}}""" + "\n");
        using var output = new StringWriter();

        await controller.ServeAsync(input, output, CancellationToken.None);

        Assert.That(output.ToString(), Does.Contain("end_turn"));
    }

    [Test]
    public async Task ServeAsync_forwardsAPermissionTurn()
    {
        var controller = new ScriptedAcpController(new ScriptedAcpService(new FixedScript(ScriptedTurnKind.Permission)));
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"session/prompt","params":{}}""" + "\n");
        using var output = new StringWriter();

        await controller.ServeAsync(input, output, CancellationToken.None);

        Assert.That(output.ToString(), Does.Contain("session/request_permission"));
    }

    private sealed class FixedScript(ScriptedTurnKind kind) : IScriptedTurnScript
    {
        public ScriptedAgentScript Load() => new([new ScriptedTurnBuilder(kind).Build()]);
    }
}
