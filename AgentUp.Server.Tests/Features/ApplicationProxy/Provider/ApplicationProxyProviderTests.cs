using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.ApplicationProxy.Models;
using AgentUp.Server.Features.ApplicationProxy.Providers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.ApplicationProxy.Provider;

[TestFixture]
public sealed class ApplicationProxyTicketStoreTests
{
    [Test]
    public void Consume_returnsTheSessionOnce()
    {
        var store = new ApplicationProxyTicketStore();
        var session = SessionFactory.Session(1);
        var ticket = store.Issue(session);

        Assert.That(store.Consume(ticket, session.ExpiresAt.AddMinutes(-1))!.AllocatedPort, Is.EqualTo(1));
        Assert.That(store.Consume(ticket, session.ExpiresAt.AddMinutes(-1)), Is.Null);
    }

    [Test]
    public void Consume_rejectsExpiredTickets()
    {
        var store = new ApplicationProxyTicketStore();
        var session = SessionFactory.Session(2);
        var ticket = store.Issue(session);

        Assert.That(store.Consume(ticket, session.ExpiresAt.AddSeconds(1)), Is.Null);
    }
}

[TestFixture]
public sealed class ApplicationProxyCookieProtectorTests
{
    [Test]
    public void Unprotect_roundTripsASessionAndRejectsTampering()
    {
        var protector = new ApplicationProxyCookieProtector(new EphemeralDataProtectionProvider());
        var session = SessionFactory.Session(8080);
        var token = protector.Protect(session);

        Assert.That(protector.Unprotect(token, session.ExpiresAt.AddMinutes(-1))!.WorkspaceId, Is.EqualTo(session.WorkspaceId));
        Assert.That(protector.Unprotect(token + "x", session.ExpiresAt.AddMinutes(-1)), Is.Null);
        Assert.That(protector.Unprotect(token, session.ExpiresAt.AddSeconds(1)), Is.Null);
    }

    [Test]
    public void Unprotect_rejectsEmptyAndMalformedValues()
    {
        var protector = new ApplicationProxyCookieProtector(new EphemeralDataProtectionProvider());

        Assert.That(protector.Unprotect(null, DateTimeOffset.UtcNow), Is.Null);
        Assert.That(protector.Unprotect("", DateTimeOffset.UtcNow), Is.Null);
        Assert.That(protector.Unprotect("not-protected", DateTimeOffset.UtcNow), Is.Null);
    }
}

[TestFixture]
public sealed class LoopbackHttpPortProbeTests
{
    [Test]
    public void IsListening_isTrueOnlyWhileALoopbackSocketAcceptsConnections()
    {
        var probe = new LoopbackHttpPortProbe();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Assert.That(probe.IsListening(port), Is.True);
        listener.Stop();
        Assert.That(probe.IsListening(port), Is.False);
    }

    [Test]
    public void IsListening_rejectsOutOfRangePorts()
    {
        Assert.That(new LoopbackHttpPortProbe().IsListening(0), Is.False);
    }
}

[TestFixture]
public sealed class ApplicationProxyOriginMapperTests
{
    [Test]
    public void ShouldRedirectToOrigin_isTrueForSafeDocumentMethods()
    {
        var mapper = new ApplicationProxyOriginMapper();
        var get = new DefaultHttpContext();
        get.Request.Method = HttpMethods.Get;
        var post = new DefaultHttpContext();
        post.Request.Method = HttpMethods.Post;

        Assert.That(mapper.ShouldRedirectToOrigin(get), Is.True);
        Assert.That(mapper.ShouldRedirectToOrigin(post), Is.False);
    }

    [Test]
    public void OriginRelativeUrl_preservesTheApplicationPathAndRemainingQuery()
    {
        var mapper = new ApplicationProxyOriginMapper();
        var context = new DefaultHttpContext();
        context.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["q"] = "1" });

        Assert.That(mapper.OriginRelativeUrl(context, "login"), Is.EqualTo("/login?q=1"));
        mapper.ApplyApplicationPath(context, "assets/app.js");
        Assert.That(context.Request.Path.Value, Is.EqualTo("/assets/app.js"));
    }
}

[TestFixture]
public sealed class ApplicationProxyCsrfGuardTests
{
    [Test]
    public void IsForeignOrigin_ignoresSafeMethodsAndSameHostWrites()
    {
        var guard = new ApplicationProxyCsrfGuard();
        var get = WriteContext(HttpMethods.Get, "https://evil.example");
        var same = WriteContext(HttpMethods.Post, "https://agent.example");
        same.Request.Host = new HostString("agent.example");

        Assert.That(guard.IsForeignOrigin(get), Is.False);
        Assert.That(guard.IsForeignOrigin(same), Is.False);
    }

    [Test]
    public void IsForeignOrigin_blocksCrossSiteWrites()
    {
        var guard = new ApplicationProxyCsrfGuard();
        var context = WriteContext(HttpMethods.Post, "https://evil.example");
        context.Request.Host = new HostString("agent.example");

        Assert.That(guard.IsForeignOrigin(context), Is.True);
    }

    private static DefaultHttpContext WriteContext(string method, string origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Headers.Origin = origin;
        return context;
    }
}

[TestFixture]
public sealed class ApplicationProxyRequestTransformerTests
{
    [Test]
    public async Task TransformRequestAsync_stripsAgentUpCredentials()
    {
        var transformer = new ApplicationProxyRequestTransformer();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/";
        context.Request.Headers.Authorization = "Bearer secret";
        context.Request.Headers.Cookie = "agent-up-proxy=hidden; theme=dark";
        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:10100/");

        await transformer.TransformRequestAsync(context, proxyRequest, "http://127.0.0.1:10100", CancellationToken.None);

        Assert.That(proxyRequest.Headers.Contains("Authorization"), Is.False);
        Assert.That(string.Join("; ", proxyRequest.Headers.GetValues("Cookie")), Is.EqualTo("theme=dark"));
    }

    [Test]
    public async Task TransformResponseAsync_rewritesLoopbackLocationsAndStripsFrameAncestors()
    {
        var transformer = new ApplicationProxyRequestTransformer();
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("agent.example");
        context.Items[ApplicationProxyConstants.DestinationPortItem] = 10100;
        using var proxyResponse = new HttpResponseMessage(HttpStatusCode.Found);
        proxyResponse.Headers.Location = new Uri("http://127.0.0.1:10100/login");
        proxyResponse.Headers.TryAddWithoutValidation("X-Frame-Options", "DENY");
        proxyResponse.Headers.TryAddWithoutValidation("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'");
        proxyResponse.Headers.TryAddWithoutValidation("Set-Cookie", "sid=1; Domain=127.0.0.1; Path=/");

        await transformer.TransformResponseAsync(context, proxyResponse, CancellationToken.None);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("https://agent.example/login"));
        Assert.That(context.Response.Headers.ContainsKey("X-Frame-Options"), Is.False);
        Assert.That(context.Response.Headers.ContentSecurityPolicy.ToString(), Does.Not.Contain("frame-ancestors"));
        Assert.That(context.Response.Headers.SetCookie.ToString(), Does.Not.Contain("Domain="));
    }
}

file static class SessionFactory
{
    public static ApplicationProxySession Session(int port)
        => new()
        {
            WorkspaceId = "workspace-1",
            AllocatedPort = port,
            ExpiresAt = new DateTimeOffset(2026, 9, 13, 13, 0, 0, TimeSpan.Zero)
        };
}
