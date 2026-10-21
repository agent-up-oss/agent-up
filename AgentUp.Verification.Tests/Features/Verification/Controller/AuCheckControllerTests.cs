using AgentUp.Verification.Features.Verification.Controllers;
using AgentUp.Verification.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Tests.Features.Verification.Controller;

[TestFixture]
public sealed class AuCheckControllerTests
{
    [Test]
    public async Task RunAsync_returnsTheExitCodeFromTheCommand()
    {
        var code = await new AuCheckController(new ScriptedCommands(2)).RunAsync(Invocation());

        Assert.That(code, Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_forwardsTheInvocation()
    {
        var commands = new ScriptedCommands(0);
        var invocation = Invocation();

        await new AuCheckController(commands).RunAsync(invocation);

        Assert.That(commands.Seen, Is.SameAs(invocation));
    }

    private static AuCheckInvocation Invocation()
        => new("plan", null, "/repo", "/repo/agent-up.json", false, null, false, null, null);

    private sealed class ScriptedCommands(int code) : IAuCheckCommands
    {
        public AuCheckInvocation? Seen { get; private set; }

        public Task<int> ExecuteAsync(AuCheckInvocation invocation, CancellationToken cancellationToken)
        {
            Seen = invocation;
            return Task.FromResult(code);
        }
    }
}
