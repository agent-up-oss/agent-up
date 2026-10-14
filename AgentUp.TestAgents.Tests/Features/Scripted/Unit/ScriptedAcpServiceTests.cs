using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;
using AgentUp.TestAgents.Features.Scripted.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Scripted.Unit;

[TestFixture]
public sealed class ScriptedAcpServiceTests
{
    [TestCase(ScriptedTurnKind.Complete, "end_turn")]
    [TestCase(ScriptedTurnKind.EndTurn, "end_turn")]
    [TestCase(ScriptedTurnKind.RateLimited, "rate_limited")]
    public async Task PlayAsync_emitsTheDeclaredStopReason(ScriptedTurnKind kind, string stopReason)
    {
        var output = await PlayAsync(Turn(kind));

        Assert.That(output, Does.Contain($"\"stopReason\":\"{stopReason}\""));
    }

    [Test]
    public async Task PlayAsync_permissionRequestsAnAllowRejectPair()
    {
        var output = await PlayAsync(new ScriptedTurnBuilder(ScriptedTurnKind.Permission).WithToolKind("edit").WithPath("src/app.ts").Build());

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("session/request_permission"));
            Assert.That(output, Does.Contain("allow-once"));
            Assert.That(output, Does.Contain("src/app.ts"));
        });
    }

    [Test]
    public async Task PlayAsync_duplicatePermissionRepeatsTheRequest()
    {
        var output = await PlayAsync(Turn(ScriptedTurnKind.DuplicatePermission));

        Assert.That(CountOccurrences(output, "session/request_permission"), Is.EqualTo(2));
    }

    [Test]
    public async Task PlayAsync_toolCallNotifiesThenCompletes()
    {
        var output = await PlayAsync(new ScriptedTurnBuilder(ScriptedTurnKind.ToolCall).WithName("read_file").Build());

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("\"sessionUpdate\":\"tool_call\""));
            Assert.That(output, Does.Contain("read_file"));
            Assert.That(output, Does.Contain("end_turn"));
        });
    }

    [Test]
    public async Task PlayAsync_toolErrorMarksTheCallFailed()
    {
        var output = await PlayAsync(new ScriptedTurnBuilder(ScriptedTurnKind.ToolError).WithName("read_file").WithError("ENOENT").Build());

        Assert.That(output, Does.Contain("ENOENT"));
        Assert.That(output, Does.Contain("failed"));
    }

    [Test]
    public async Task PlayAsync_unknownToolStillCompletesTheTurn()
    {
        var output = await PlayAsync(new ScriptedTurnBuilder(ScriptedTurnKind.UnknownTool).WithName("explode").Build());

        Assert.That(output, Does.Contain("explode"));
        Assert.That(output, Does.Contain("end_turn"));
    }

    [Test]
    public async Task PlayAsync_emptyAndWhitespaceProduceNoJsonRpcResult()
    {
        var empty = await PlayAsync(Turn(ScriptedTurnKind.Empty));
        var whitespace = await PlayAsync(Turn(ScriptedTurnKind.Whitespace));

        Assert.That(empty, Does.Not.Contain("session/prompt"));
        Assert.That(whitespace.Trim(), Does.Not.Contain("stopReason"));
    }

    [Test]
    public async Task PlayAsync_truncatedJsonLeavesAnOpenBrace()
    {
        var output = await PlayAsync(Turn(ScriptedTurnKind.TruncatedJson));

        Assert.That(output, Does.Contain("{\"jsonrpc\":\"2.0\",\"id\":"));
        Assert.That(output.Trim().EndsWith('}'), Is.False);
    }

    [Test]
    public async Task PlayAsync_outOfOrderCompletesThenRequestsPermission()
    {
        var output = await PlayAsync(Turn(ScriptedTurnKind.OutOfOrder));
        var complete = output.IndexOf("end_turn", StringComparison.Ordinal);
        var permission = output.IndexOf("session/request_permission", StringComparison.Ordinal);

        Assert.That(complete, Is.GreaterThanOrEqualTo(0));
        Assert.That(permission, Is.GreaterThan(complete));
    }

    [Test]
    public void PlayAsync_crashThrowsInsteadOfExitingTheHost()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            PlayAsync(Turn(ScriptedTurnKind.Crash)));

        Assert.That(exception!.Message, Is.EqualTo("scripted crash"));
    }

    [Test]
    public void PlayAsync_holdPipeHonorsCancellation()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        Assert.CatchAsync<OperationCanceledException>(() =>
            PlayAsync(Turn(ScriptedTurnKind.HoldPipe), lifetime.Token));
    }

    [Test]
    public async Task PlayAsync_hugeArgsCarryAMultiMegabytePayload()
    {
        var output = await PlayAsync(new ScriptedTurnBuilder(ScriptedTurnKind.HugeArgs).WithName("write_file").Build());

        Assert.That(output.Length, Is.GreaterThan(3_000_000));
    }

    [Test]
    public async Task PlayAsync_invalidUtf8StillCompletes()
    {
        var output = await PlayAsync(Turn(ScriptedTurnKind.InvalidUtf8));

        Assert.That(output, Does.Contain("end_turn"));
    }

    [Test]
    public async Task Initialize_advertisesAnEmptyAuthMethodList()
    {
        var output = await PlayAsync(Turn(ScriptedTurnKind.Complete));

        Assert.That(output, Does.Contain("\"authMethods\":[]"));
    }

    [Test]
    public async Task RunAsync_skipsMalformedFramesAndHonorsCancel()
    {
        var service = new ScriptedAcpService(new FixedScript(Turn(ScriptedTurnKind.Complete)));
        using var input = new StringReader(
            "not-json\n"
            + """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n"
            + """{"jsonrpc":"2.0","id":2,"method":"session/cancel","params":{}}""" + "\n"
            + """{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":"hi"}}""" + "\n");
        using var output = new StringWriter();

        await service.RunAsync(input, output, CancellationToken.None);

        Assert.That(output.ToString(), Does.Contain("end_turn"));
        Assert.That(ScriptedAgentScript.Empty.Turns, Is.Empty);
    }

    [Test]
    public async Task PlayAsync_unknownKindCompletesTheTurn()
    {
        var output = await PlayAsync(new ScriptedTurnBuilder((ScriptedTurnKind)999).Build());

        Assert.That(output, Does.Contain("end_turn"));
    }

    private static ScriptedTurn Turn(ScriptedTurnKind kind) => new ScriptedTurnBuilder(kind).Build();

    private static async Task<string> PlayAsync(ScriptedTurn turn, CancellationToken cancellationToken = default)
    {
        var service = new ScriptedAcpService(new FixedScript(turn));
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n"
            + """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{}}""" + "\n"
            + """{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":"hi"}}""" + "\n");
        using var output = new StringWriter();
        await service.RunAsync(input, output, cancellationToken);
        return output.ToString();
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var index = 0; (index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0; index += needle.Length)
            count++;
        return count;
    }

    private sealed class FixedScript(ScriptedTurn turn) : IScriptedTurnScript
    {
        public ScriptedAgentScript Load() => new([turn]);
    }
}
