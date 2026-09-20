using AgentUp.Server.Features.Connection.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Connection.Provider;

[TestFixture]
public sealed class ConnectionMetadataProviderTests
{
    [Test]
    public void Current_DefaultsToLocalConnection()
    {
        var dto = new ConnectionMetadataProvider(new ConfigurationBuilder().Build()).Current();
        Assert.Multiple(() =>
        {
            Assert.That(dto.ConnectionId, Is.EqualTo("local"));
            Assert.That(dto.Kind, Is.EqualTo("selfHosted"));
            Assert.That(dto.Authentication.IdentifierRequired, Is.False);
        });
    }

    [Test]
    public void Current_UsesAdministratorPromptForLocalMode()
    {
        var dto = new ConnectionMetadataProvider(new ConfigurationBuilder().Build()).Current();
        Assert.That(dto.Authentication.Prompt, Does.Contain("administrator password"));
    }

    [Test]
    public void Current_ReportsDisabledModeFromAuthMode()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_AUTH_MODE"] = "disabled" }).Build();
        var dto = new ConnectionMetadataProvider(configuration).Current();
        Assert.Multiple(() =>
        {
            Assert.That(dto.Authentication.Mode, Is.EqualTo("disabled"));
            Assert.That(dto.Authentication.Prompt, Does.Contain("not required"));
        });
    }
}
