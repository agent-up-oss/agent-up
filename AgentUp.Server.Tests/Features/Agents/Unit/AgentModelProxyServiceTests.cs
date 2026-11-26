using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

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

    [Test]
    public void WriteUnavailable_abortsAfterHeadersHaveStarted()
    {
        var context = new DefaultHttpContext();
        var lifetime = new Lifetime();
        context.Features.Set<IHttpResponseFeature>(new StartedResponse());
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);

        AgentModelProxyService.WriteUnavailable(context);

        Assert.Multiple(() =>
        {
            Assert.That(lifetime.Aborted, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
        });
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

    private sealed class StartedResponse : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    private sealed class Lifetime : IHttpRequestLifetimeFeature
    {
        public bool Aborted { get; private set; }
        public CancellationToken RequestAborted { get; set; }
        public void Abort() => Aborted = true;
    }
}
