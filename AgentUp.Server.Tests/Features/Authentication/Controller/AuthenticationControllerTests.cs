using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Controller;

[TestFixture]
public class AuthenticationControllerTests
{
    [Test]
    public void Login_ReturnsTokenForAdminPassword()
    {
        var controller = new AuthenticationController(CreateService(("AGENTUP_ADMIN_PASSWORD", "secret")));

        var response = controller.Login(new LoginRequest("secret"));

        Assert.That(response.Value?.AccessToken, Is.Not.Empty);
    }

    [Test]
    public void Login_ReturnsProblemForWrongPassword()
    {
        var controller = new AuthenticationController(CreateService(("AGENTUP_ADMIN_PASSWORD", "secret")));

        var response = controller.Login(new LoginRequest("wrong"));

        Assert.That(response.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)response.Result!).StatusCode, Is.EqualTo(401));
    }

    private static AuthenticationService CreateService(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(value => value.Key, value => (string?)value.Value)).Build();
        return new AuthenticationService(new AuthenticationProvider(configuration), new AuthenticationModeProvider(configuration));
    }
}
