using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Tests.Features.Acp.Unit;

[TestFixture]
public sealed class AcpAgentServiceTests
{
    [Test]
    public void MethodIds_mirrorTheRealAgentsTheyStandInFor()
    {
        Assert.Multiple(() =>
        {
            // The Server filters auth methods by id and name, so these have to match the shapes
            // it sees in production or the test agents would exercise a different branch.
            Assert.That(AcpAgentService.MethodId(TestAgentSchema.DeviceCode), Is.EqualTo("chatgpt"));
            Assert.That(AcpAgentService.MethodId(TestAgentSchema.LoopbackRedirect), Is.EqualTo("chatgpt"));
            Assert.That(AcpAgentService.MethodId(TestAgentSchema.PastedCode), Is.EqualTo("claude-login"));
            Assert.That(AcpAgentService.MethodId(TestAgentSchema.SilentPoll), Is.EqualTo("cursor_login"));
            Assert.That(AcpAgentService.MethodName(TestAgentSchema.PastedCode), Is.EqualTo("Claude Pro"));
        });
    }

    [Test]
    public async Task Initialize_advertisesProtocolOneAndExactlyOneSubscriptionMethod()
    {
        var response = await ExchangeAsync(
            TestAgentSchema.DeviceCode,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");

        var result = response!["result"]!;
        var methods = result["authMethods"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(result["protocolVersion"]!.GetValue<int>(), Is.EqualTo(1));
            Assert.That(methods, Has.Count.EqualTo(1));
            Assert.That(methods[0]!["id"]!.GetValue<string>(), Is.EqualTo("chatgpt"));
        });
    }

    // The refusal is what drives the Server into authentication_required, so the whole sign-in
    // path downstream of it only runs because this happens.
    [Test]
    public async Task NewSession_isRefusedUntilTheAgentHasACredential()
    {
        var response = await ExchangeAsync(
            TestAgentSchema.DeviceCode,
            """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"/tmp"}}""");

        var error = response!["error"]!;

        Assert.Multiple(() =>
        {
            Assert.That(response["result"], Is.Null);
            Assert.That(error["message"]!.GetValue<string>(), Does.Contain("Authentication required"));
        });
    }

    [Test]
    public async Task NewSession_succeedsOnceACredentialExists()
    {
        var response = await ExchangeAsync(
            TestAgentSchema.DeviceCode,
            """{"jsonrpc":"2.0","id":3,"method":"session/new","params":{"cwd":"/tmp"}}""",
            "test-oat-already-signed-in");

        Assert.That(response!["result"]!["sessionId"]!.GetValue<string>(), Does.StartWith("test-session-"));
    }

    [Test]
    public async Task Prompt_endsTheTurn()
    {
        var response = await ExchangeAsync(
            TestAgentSchema.DeviceCode,
            """{"jsonrpc":"2.0","id":4,"method":"session/prompt","params":{}}""",
            "test-oat-already-signed-in");

        Assert.That(response!["result"]!["stopReason"]!.GetValue<string>(), Is.EqualTo("end_turn"));
    }

    [Test]
    public async Task Run_ignoresBlankAndUnparseableFrames()
    {
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await new AcpAgentService().RunAsync(
            TestAgentSchema.DeviceCode,
            new FakeCredentialStore(null),
            new StringReader("\n{ not json }\n"),
            output,
            cancellation.Token);

        Assert.That(output.ToString(), Is.Empty);
    }

    private static async Task<JsonNode?> ExchangeAsync(TestAgentSchema schema, string frame, string? credential = null)
    {
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await new AcpAgentService().RunAsync(
            schema,
            new FakeCredentialStore(credential),
            new StringReader(frame + "\n"),
            output,
            cancellation.Token);
        var written = output.ToString().Trim();
        return written.Length == 0 ? null : JsonNode.Parse(written);
    }

    /// <summary>Keeps this unit test off the real home directory.</summary>
    private sealed class FakeCredentialStore(string? stored) : ITestAgentCredentialStore
    {
        public string? Read() => stored;

        public void Write(string token) => throw new NotSupportedException("This test never signs in.");
    }
}
