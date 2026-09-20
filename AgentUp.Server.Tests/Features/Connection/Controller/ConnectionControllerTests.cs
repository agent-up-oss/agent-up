using AgentUp.Server.Features.Connection.Controllers;
using AgentUp.Server.Features.Connection.Providers;
using AgentUp.Server.Features.Connection.Services;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Connection.Controller;

[TestFixture]
public sealed class ConnectionControllerTests
{
    [Test]
    public void Get_ReturnsSelfHostedLocalAdministratorConnection()
    {
        var dto = new ConnectionController(CreateService()).Get().Value;
        Assert.Multiple(() =>
        {
            Assert.That(dto!.Kind, Is.EqualTo("selfHosted"));
            Assert.That(dto.WorkspacePresentation, Is.EqualTo("serverScoped"));
            Assert.That(dto.Authentication.Mode, Is.EqualTo("localAdministrator"));
        });
    }

    [Test]
    public void Get_ReportsDisabledAuthenticationWhenConfigured()
    {
        var dto = new ConnectionController(CreateService(("AGENTUP_AUTH_DISABLED", "true"))).Get().Value;
        Assert.That(dto!.Authentication.Mode, Is.EqualTo("disabled"));
    }

    private static ConnectionService CreateService(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(value => value.Key, value => (string?)value.Value)).Build();
        return new ConnectionService(new ConnectionMetadataProvider(configuration));
    }
}
