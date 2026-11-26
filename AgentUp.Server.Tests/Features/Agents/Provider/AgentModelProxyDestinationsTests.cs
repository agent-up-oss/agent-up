using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentModelProxyDestinationsTests
{
    [Test]
    public void Resolve_usesTheStoredOpencodeEndpointAndKeepsItsTokenOnTheServer()
    {
        var settings = new CapabilityModuleHarness.MemorySettings();
        settings.Write("opencode", new Dictionary<string, string>
        {
            ["endpoint"] = "http://ollama.test:11434/v1",
            ["token"] = "sk-real"
        });
        var destinations = Destinations(settings);

        var target = destinations.Resolve("opencode");

        Assert.Multiple(() =>
        {
            Assert.That(target!.Origin, Is.EqualTo("http://ollama.test:11434"));
            Assert.That(target.BearerToken, Is.EqualTo("sk-real"));
        });
    }

    [Test]
    public void Resolve_usesVendorOriginsForClaudeCodexAndCursor()
    {
        var destinations = Destinations();

        Assert.Multiple(() =>
        {
            Assert.That(destinations.Resolve("claude")!.Origin, Is.EqualTo(AgentModelProxyDestinations.ClaudeOrigin));
            Assert.That(destinations.Resolve("codex")!.Origin, Is.EqualTo(AgentModelProxyDestinations.CodexOrigin));
            Assert.That(destinations.Resolve("cursor")!.Origin, Is.EqualTo(AgentModelProxyDestinations.CursorOrigin));
            Assert.That(destinations.Resolve("claude")!.BearerToken, Is.Null);
        });
    }

    [Test]
    public void Resolve_usesTheConfiguredOllamaHost()
    {
        var destinations = Destinations(ollama: "http://models.test:21434");

        Assert.That(destinations.Resolve("ollama")!.Origin, Is.EqualTo("http://models.test:21434"));
    }

    [Test]
    public void Resolve_returnsNullForAnUnknownCapability()
    {
        Assert.That(Destinations().Resolve("missing"), Is.Null);
    }

    private static AgentModelProxyDestinations Destinations(
        CapabilityModuleHarness.MemorySettings? settings = null,
        string? ollama = null)
    {
        var modules = CapabilityModuleHarness.CreateService(
            enabled: true,
            modules: [new OpencodeAgentCapability()],
            nixAvailable: false,
            settings: settings ?? new CapabilityModuleHarness.MemorySettings());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ollama is null
                ? []
                : new Dictionary<string, string?> { ["AGENTUP_OLLAMA_URL"] = ollama })
            .Build();
        return new AgentModelProxyDestinations(new CapabilityModulesController(modules), configuration);
    }
}
