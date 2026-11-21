using System.Text.Json;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentMcpServerProviderTests
{
    [Test]
    public void Servers_lists_every_named_endpoint_on_loopback_for_an_http_capable_agent()
    {
        var servers = JsonSerializer.SerializeToElement(Provider("http://0.0.0.0:5001").Servers(httpSupported: true));

        Assert.Multiple(() =>
        {
            Assert.That(servers.GetArrayLength(), Is.EqualTo(AgentMcpServerProvider.Endpoints.Count));
            Assert.That(servers[0].GetProperty("type").GetString(), Is.EqualTo("http"));
            // The task tools lead: they are how an agent reads its task and says it is done.
            Assert.That(servers[0].GetProperty("name").GetString(), Is.EqualTo("agent-up-tasks"));
            Assert.That(servers[0].GetProperty("url").GetString(), Is.EqualTo("http://127.0.0.1:5001/mcp/tasks"));
            Assert.That(servers[0].GetProperty("headers").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public void Servers_carry_the_agent_sessions_credential_on_every_endpoint()
    {
        var servers = JsonSerializer.SerializeToElement(Provider("http://localhost:5001").Servers(httpSupported: true, sessionToken: "token-1"));

        Assert.That(
            servers.EnumerateArray().Select(server => server.GetProperty("headers")[0].GetProperty("value").GetString()),
            Has.All.EqualTo("token-1"));
    }

    [Test]
    public void Servers_offers_nothing_to_an_agent_without_http_mcp()
    {
        Assert.That(Provider("http://localhost:5001").Servers(httpSupported: false), Is.Empty);
    }

    [TestCase("http://*:5000", "http://127.0.0.1:5000")]
    [TestCase("http://+:5000", "http://127.0.0.1:5000")]
    [TestCase("http://[::]:5002", "http://127.0.0.1:5002")]
    [TestCase("http://localhost:5001;https://localhost:5443", "http://localhost:5001")]
    [TestCase("", "http://127.0.0.1:5000")]
    public void LoopbackBaseUrl_turns_a_wildcard_binding_into_loopback(string urls, string expected)
    {
        Assert.That(Provider(urls).LoopbackBaseUrl(), Is.EqualTo(expected));
    }

    [Test]
    public void SupportsHttp_reads_the_agent_initialize_capabilities()
    {
        var http = JsonSerializer.SerializeToElement(new { agentCapabilities = new { mcpCapabilities = new { http = true, sse = true } } });
        var stdioOnly = JsonSerializer.SerializeToElement(new { agentCapabilities = new { mcpCapabilities = new { http = false } } });
        var none = JsonSerializer.SerializeToElement(new { protocolVersion = 1 });

        Assert.Multiple(() =>
        {
            Assert.That(AgentMcpServerProvider.SupportsHttp(http), Is.True);
            Assert.That(AgentMcpServerProvider.SupportsHttp(stdioOnly), Is.False);
            Assert.That(AgentMcpServerProvider.SupportsHttp(none), Is.False);
        });
    }

    private static AgentMcpServerProvider Provider(string urls) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = urls }).Build());
}
