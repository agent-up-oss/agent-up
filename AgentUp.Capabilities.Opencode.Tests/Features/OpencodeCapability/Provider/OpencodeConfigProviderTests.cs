using System.Text.Json;
using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Providers;

namespace AgentUp.Capabilities.Opencode.Tests.Features.OpencodeCapability.Provider;

[TestFixture]
public sealed class OpencodeConfigProviderTests
{
    [TestCase("http://localhost:11434", "http://localhost:11434/v1")]
    [TestCase("http://localhost:11434/", "http://localhost:11434/v1")]
    [TestCase("https://ollama.example.test/v1", "https://ollama.example.test/v1")]
    [TestCase("https://gateway.example.test/ollama/v1/", "https://gateway.example.test/ollama/v1")]
    public void BaseUrl_adds_the_openai_compatible_path_only_to_a_bare_host(string endpoint, string expected)
    {
        Assert.That(OpencodeConfigProvider.BaseUrl(endpoint), Is.EqualTo(expected));
    }

    [Test]
    public void Write_uses_the_openai_compatible_provider_package()
    {
        var config = JsonDocument.Parse(new OpencodeConfigProvider().Write("http://localhost:11434", "llama3", hasToken: false)).RootElement;
        var provider = config.GetProperty("provider").GetProperty("agent-up");

        Assert.That(provider.GetProperty("npm").GetString(), Is.EqualTo("@ai-sdk/openai-compatible"));
        Assert.That(provider.GetProperty("models").TryGetProperty("llama3", out _), Is.True);
    }
}
