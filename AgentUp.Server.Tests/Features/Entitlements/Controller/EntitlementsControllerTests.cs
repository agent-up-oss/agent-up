using System.Security.Claims;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Entitlements.Controllers;
using AgentUp.Server.Features.Entitlements.Providers;
using AgentUp.Server.Features.Entitlements.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Entitlements.Controller;

[TestFixture]
public sealed class EntitlementsControllerTests
{
    [Test]
    public void Get_ReturnsCommunityEditionForTheCaller()
    {
        var dto = CreateController().Get().Value;
        Assert.Multiple(() =>
        {
            Assert.That(dto!.Source, Is.EqualTo("selfHosted"));
            Assert.That(dto.Edition, Is.EqualTo("community"));
            Assert.That(dto.Billing, Is.EqualTo("free"));
            Assert.That(dto.Features[OperationPermissions.AgentPrompt].Available, Is.True);
        });
    }

    [Test]
    public void Get_UnlocksEveryOperationPermission()
    {
        var dto = CreateController().Get().Value;
        Assert.That(dto!.Features.Keys, Is.EquivalentTo(OperationPermissions.All));
    }

    [Test]
    public void Get_FallsBackToTheIdentityNameWhenNameIdentifierIsMissing()
    {
        var dto = CreateController(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "named-user")]))).Get().Value;
        Assert.That(dto!.Subject, Is.EqualTo("named-user"));
    }

    [Test]
    public void Get_FallsBackToAdminWhenTheCallerHasNoName()
    {
        var dto = CreateController(new ClaimsPrincipal(new ClaimsIdentity())).Get().Value;
        Assert.That(dto!.Subject, Is.EqualTo("admin"));
    }

    private static EntitlementsController CreateController(ClaimsPrincipal? user = null)
    {
        var controller = new EntitlementsController(
            new EntitlementsService(new SelfHostedEntitlementsProvider(new ConfigurationBuilder().Build())));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")]))
            }
        };
        return controller;
    }
}
