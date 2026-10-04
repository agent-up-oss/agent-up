using System.Net;
using System.Security.Claims;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Models;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class McpEndpointAuthorizationHandlerTests
{
    private const string RemoteAddress = "203.0.113.10";

    [Test]
    public async Task ALoopbackCallerSatisfiesTheEndpointWithoutAnyClaims()
    {
        var context = Evaluate("127.0.0.1", permissions: []);

        await Handler(context.Http).HandleAsync(context.Authorization);

        Assert.That(context.Authorization.HasSucceeded, Is.True);
    }

    [Test]
    public async Task ACallerWithoutARemoteAddressIsTreatedAsLoopback()
    {
        var context = Evaluate(remoteAddress: null, permissions: []);

        await Handler(context.Http).HandleAsync(context.Authorization);

        Assert.That(context.Authorization.HasSucceeded, Is.True);
    }

    [Test]
    public async Task ARemoteCallerCarryingThePermissionSatisfiesTheEndpoint()
    {
        var context = Evaluate(RemoteAddress, [OperationPermissions.BrowserControl]);

        await Handler(context.Http).HandleAsync(context.Authorization);

        Assert.That(context.Authorization.HasSucceeded, Is.True);
    }

    [Test]
    public async Task ARemoteCallerCarryingAnotherPermissionIsRefused()
    {
        var context = Evaluate(RemoteAddress, [OperationPermissions.GitRead]);

        await Handler(context.Http).HandleAsync(context.Authorization);

        Assert.That(context.Authorization.HasSucceeded, Is.False);
    }

    [Test]
    public async Task ARemoteCallerWithNoPermissionsIsRefused()
    {
        var context = Evaluate(RemoteAddress, permissions: []);

        await Handler(context.Http).HandleAsync(context.Authorization);

        Assert.That(context.Authorization.HasSucceeded, Is.False);
    }

    [Test]
    public async Task AHandlerWithNoRequestCannotClaimLoopback()
    {
        var context = Evaluate(RemoteAddress, permissions: []);

        await new McpEndpointAuthorizationHandler(new HttpContextAccessor())
            .HandleAsync(context.Authorization);

        Assert.That(context.Authorization.HasSucceeded, Is.False);
    }

    private static McpEndpointAuthorizationHandler Handler(HttpContext http)
        => new(new HttpContextAccessor { HttpContext = http });

    private static (HttpContext Http, AuthorizationHandlerContext Authorization) Evaluate(
        string? remoteAddress,
        IReadOnlyList<string> permissions)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = remoteAddress is null ? null : IPAddress.Parse(remoteAddress);
        var claims = permissions
            .Select(permission => new Claim(McpEndpointAuthorizationHandler.PermissionClaimType, permission))
            .ToArray();
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme"));
        var requirement = new McpLoopbackOrPermissionRequirement(OperationPermissions.BrowserControl);
        return (http, new AuthorizationHandlerContext([requirement], user, resource: null));
    }
}
