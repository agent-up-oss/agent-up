using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentModelProxyServiceTests
{
    [Test]
    public async Task ForwardAsync_rejectsAnUnknownKey()
    {
        var access = new AgentModelProxyAccess();
        var service = new AgentModelProxyService(access, new FixedDestinations(null), new AgentModelProxyForwarder());
        var context = new DefaultHttpContext();

        await service.ForwardAsync("wrong-key", "claude", "v1/messages", context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task ForwardAsync_rejectsAnUnknownCapability()
    {
        var access = new AgentModelProxyAccess();
        var service = new AgentModelProxyService(access, new FixedDestinations(null), new AgentModelProxyForwarder());
        var context = new DefaultHttpContext();

        await service.ForwardAsync(access.Key, "missing", "v1/messages", context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task ForwardAsync_rejectsAnUnusableOrigin()
    {
        var access = new AgentModelProxyAccess();
        var service = new AgentModelProxyService(
            access,
            new FixedDestinations(new AgentModelProxyTarget("not a uri", null)),
            new AgentModelProxyForwarder());
        var context = new DefaultHttpContext();

        await service.ForwardAsync(access.Key, "claude", "v1/messages", context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task ForwardAsync_reportsUnavailableWhenTheOriginCannotBeReached()
    {
        var access = new AgentModelProxyAccess();
        var service = new AgentModelProxyService(
            access,
            new FixedDestinations(new AgentModelProxyTarget("http://origin.test", "sk")),
            new AgentModelProxyForwarder(new ThrowingHandler()));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;

        await service.ForwardAsync(access.Key, "opencode", "v1/models", context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status502BadGateway));
    }

    private sealed class FixedDestinations(AgentModelProxyTarget? target) : IAgentModelProxyDestinations
    {
        public AgentModelProxyTarget? Resolve(string capability) => target;
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("origin down");
    }
}
