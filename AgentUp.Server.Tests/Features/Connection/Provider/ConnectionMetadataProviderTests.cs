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
}
