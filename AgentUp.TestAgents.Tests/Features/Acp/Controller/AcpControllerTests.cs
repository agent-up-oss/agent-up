using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Acp.Controllers;
using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Tests.Features.Acp.Controller;

// The slice's boundary, driven over the same stdio wire the Server uses: whichever agent and
// credential the host hands in has to reach the protocol unchanged.
[TestFixture]
public sealed class AcpControllerTests
{
    [Test]
    public async Task ServeAsync_advertisesTheAgentItWasAskedToServe()
    {
        var response = await ExchangeAsync(
            TestAgentSchema.PastedCode,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");

        var methods = response!["result"]!["authMethods"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(methods, Has.Count.EqualTo(1));
            Assert.That(methods[0]!["id"]!.GetValue<string>(), Is.EqualTo("claude-login"),
                "The schema passed in has to reach the protocol, or a test agent would stand in for the wrong CLI");
        });
    }

    // The refusal is what drives the Server into authentication_required, so it has to survive
    // the trip through the controller rather than only holding inside the service.
    [Test]
    public async Task ServeAsync_refusesANewSessionUntilTheCredentialItWasGivenExists()
    {
        var refused = await ExchangeAsync(
            TestAgentSchema.DeviceCode,
            """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"/tmp"}}""");

        var signedIn = await ExchangeAsync(
            TestAgentSchema.DeviceCode,
            """{"jsonrpc":"2.0","id":3,"method":"session/new","params":{"cwd":"/tmp"}}""",
            "test-oat-already-signed-in");

        Assert.Multiple(() =>
        {
            Assert.That(refused!["error"]!["message"]!.GetValue<string>(), Does.Contain("Authentication required"));
            Assert.That(signedIn!["result"]!["sessionId"]!.GetValue<string>(), Does.StartWith("test-session-"));
        });
    }

    [Test]
    public async Task ServeAsync_returnsWhenTheStreamEnds()
    {
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var input = new StringReader(string.Empty);
        await new AcpController(new AcpAgentService()).ServeAsync(
            TestAgentSchema.SilentPoll,
            new StubCredentialStore(null),
            input,
            output,
            cancellation.Token);

        Assert.That(output.ToString(), Is.Empty);
    }

    private static async Task<JsonNode?> ExchangeAsync(TestAgentSchema schema, string frame, string? credential = null)
    {
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var input = new StringReader(frame + "\n");
        await new AcpController(new AcpAgentService()).ServeAsync(
            schema,
            new StubCredentialStore(credential),
            input,
            output,
            cancellation.Token);

        var written = output.ToString().Trim();
        return written.Length == 0 ? null : JsonNode.Parse(written);
    }

    /// <summary>Keeps this test off the real home directory.</summary>
    private sealed class StubCredentialStore(string? stored) : ITestAgentCredentialStore
    {
        public string? Read() => stored;

        public void Write(string token) => throw new NotSupportedException("This test never signs in.");
    }
}
