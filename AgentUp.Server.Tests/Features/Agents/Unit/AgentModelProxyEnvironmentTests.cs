using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Providers;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentModelProxyEnvironmentTests
{
    [Test]
    public void Apply_pointsVendorAndOllamaUrlsAtTheLoopbackProxy()
    {
        var environment = AgentModelProxyEnvironment.Apply(
            new Dictionary<string, string>
            {
                [OpencodeConfigProvider.ConfigVariable] =
                    """{"provider":{"agent-up":{"options":{"baseURL":"http://ollama.test/v1"}}}}"""
            },
            "http://127.0.0.1:5001",
            "proxy-key");

        Assert.Multiple(() =>
        {
            Assert.That(environment["ANTHROPIC_BASE_URL"],
                Is.EqualTo("http://127.0.0.1:5001/api/agent-model/proxy-key/claude"));
            Assert.That(environment["OPENAI_BASE_URL"],
                Is.EqualTo("http://127.0.0.1:5001/api/agent-model/proxy-key/codex/v1"));
            Assert.That(environment["CURSOR_API_BASE_URL"],
                Is.EqualTo("http://127.0.0.1:5001/api/agent-model/proxy-key/cursor"));
            Assert.That(environment["OLLAMA_HOST"],
                Is.EqualTo("http://127.0.0.1:5001/api/agent-model/proxy-key/ollama"));
            Assert.That(environment[OpencodeConfigProvider.TokenVariable], Is.EqualTo("proxy-key"));
            Assert.That(environment[OpencodeConfigProvider.ConfigVariable],
                Does.Contain("http://127.0.0.1:5001/api/agent-model/proxy-key/opencode/v1"));
        });
    }

    [Test]
    public void RewriteOpencode_leavesADocumentWithoutTheProviderUnchanged()
    {
        Assert.That(AgentModelProxyEnvironment.RewriteOpencode("{}", "http://proxy/opencode"), Is.EqualTo("{}"));
    }
}
