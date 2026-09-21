using AgentUp.Server.Features.Connection.Providers;
using AgentUp.Server.Features.Connection.Services;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Connection.Unit;

[TestFixture]
public sealed class ConnectionServiceTests
{
    [Test]
    public void Current_UsesConfiguredConnectionIdAndDisplayName()
    {
        var dto = Create(
            ("AGENTUP_CONNECTION_ID", "workstation"),
            ("AGENTUP_CONNECTION_DISPLAY_NAME", "Dev box")).Current();

        Assert.Multiple(() =>
        {
            Assert.That(dto.ConnectionId, Is.EqualTo("workstation"));
            Assert.That(dto.DisplayName, Is.EqualTo("Dev box"));
            Assert.That(dto.ApiVersion, Is.EqualTo("1"));
        });
    }

    [Test]
    public void Current_ReportsExternalBearerMode()
    {
        var dto = Create(("AGENTUP_AUTH_MODE", "externalBearer")).Current();
        Assert.That(dto.Authentication.Mode, Is.EqualTo("externalBearer"));
    }

    private static ConnectionService Create(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(value => value.Key, value => (string?)value.Value)).Build();
        return new ConnectionService(new ConnectionMetadataProvider(configuration));
    }
}
