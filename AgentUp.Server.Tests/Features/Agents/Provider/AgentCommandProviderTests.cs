using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentCommandProviderTests
{
    [Test]
    public void Get_usesConfiguredExecutableAndArguments()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Codex:Command"] = "/tools/acp", ["Agents:Codex:Arguments:0"] = "serve"
        }).Build();
        var result = new AgentCommandProvider(configuration).Get(AgentKind.Codex);
        Assert.Multiple(() => { Assert.That(result.FileName, Is.EqualTo("/tools/acp")); Assert.That(result.Arguments, Is.EqualTo(new[] { "serve" })); });
    }
}
