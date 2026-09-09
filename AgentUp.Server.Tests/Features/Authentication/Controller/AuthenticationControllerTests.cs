using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Controller;

[TestFixture]
public class AuthenticationControllerTests
{
    [Test]
    public void Login_ReturnsTokenForAdminPassword()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "secret" }).Build();
        var controller = new AuthenticationController(new AuthenticationService(new AuthenticationProvider(configuration)));

        var response = controller.Login(new LoginRequest("secret"));

        Assert.That(response.Value?.AccessToken, Is.Not.Empty);
    }

    [Test]
    public void Login_ReturnsProblemForWrongPassword()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AGENTUP_ADMIN_PASSWORD"] = "secret" }).Build();
        var controller = new AuthenticationController(new AuthenticationService(new AuthenticationProvider(configuration)));

        var response = controller.Login(new LoginRequest("wrong"));

        Assert.That(response.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)response.Result!).StatusCode, Is.EqualTo(401));
    }
}
