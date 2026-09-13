using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.ApplicationProxy.Models;
using AgentUp.Server.Features.ApplicationProxy.Providers;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Yarp.ReverseProxy.Forwarder;

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

    [Test]
    public void Issue_evictsAbandonedExpiredTickets()
    {
        var clock = new StubTimeProvider { UtcNow = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero) };
        var store = new ApplicationProxyTicketStore(clock);
        var stale = store.Issue(new ApplicationProxySession
        {
            WorkspaceId = "workspace-1",
            AllocatedPort = 1,
            ExpiresAt = clock.UtcNow.AddSeconds(-1)
        });

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var fresh = store.Issue(SessionFactory.Session(2));

        Assert.That(store.Consume(stale, clock.UtcNow), Is.Null);
        Assert.That(store.Consume(fresh, clock.UtcNow.AddMinutes(-1))!.AllocatedPort, Is.EqualTo(2));
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

    [Test]
    public void Unprotect_rejectsMalformedPayloads()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = new ApplicationProxyCookieProtector(provider);
        var inner = provider.CreateProtector("AgentUp.ApplicationProxy.v1");
        var now = DateTimeOffset.UtcNow;

        Assert.That(protector.Unprotect(inner.Protect("workspace"), now), Is.Null);
        Assert.That(protector.Unprotect(inner.Protect("workspace\nnot-a-port\n1"), now), Is.Null);
        Assert.That(protector.Unprotect(inner.Protect("workspace\n0\n1"), now), Is.Null);
        Assert.That(protector.Unprotect(inner.Protect("workspace\n8080\nnot-a-unix-time"), now), Is.Null);
    }

    [Test]
    public void Unprotect_returnsNullWhenUnprotectThrowsFormatException()
    {
        var protector = new ApplicationProxyCookieProtector(new FormatThrowingProtection());

        Assert.That(protector.Unprotect("token", DateTimeOffset.UtcNow), Is.Null);
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

    [Test]
    public void IsListening_rejectsPortsOutsideTheTcpRange()
    {
        var probe = new LoopbackHttpPortProbe();

        Assert.That(probe.IsListening(-1), Is.False);
        Assert.That(probe.IsListening(65536), Is.False);
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

    [Test]
    public void OriginRelativeUrl_collapsesLeadingSlashesToStayOnTheServerOrigin()
    {
        var mapper = new ApplicationProxyOriginMapper();
        var context = new DefaultHttpContext();

        Assert.That(mapper.OriginRelativeUrl(context, "//evil.example/phish"), Is.EqualTo("/evil.example/phish"));
        mapper.ApplyApplicationPath(context, "//cdn.example/app.js");
        Assert.That(context.Request.Path.Value, Is.EqualTo("/cdn.example/app.js"));
        mapper.ApplyApplicationPath(context, @"..\windows\path");
        Assert.That(context.Request.Path.Value, Is.EqualTo("/"));
    }

    [Test]
    public void IsReservedFallbackPath_blocksServerOwnedPrefixes()
    {
        var mapper = new ApplicationProxyOriginMapper();
        var api = new DefaultHttpContext();
        api.Request.Path = "/api/workspaces";
        var root = new DefaultHttpContext();
        root.Request.Path = "/";

        Assert.That(mapper.IsReservedFallbackPath(api), Is.True);
        Assert.That(mapper.IsReservedFallbackPath(root), Is.False);
    }

    [Test]
    public async Task RedirectToOriginRootAsync_writesALocalRootRedirect()
    {
        var mapper = new ApplicationProxyOriginMapper();
        var context = new DefaultHttpContext();

        await mapper.RedirectToOriginRootAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/"));
    }
}

[TestFixture]
public sealed class ApplicationProxyTransportGuardTests
{
    [Test]
    public void AllowsCredentials_acceptsHttpsAndLoopbackHttp()
    {
        var guard = new ApplicationProxyTransportGuard();
        var https = new DefaultHttpContext();
        https.Request.Scheme = "https";
        https.Request.Host = new HostString("agent.example");
        var loopback = ApplicationProxyHarness.LoopbackContext();
        var remoteHttp = new DefaultHttpContext();
        remoteHttp.Request.Scheme = "http";
        remoteHttp.Request.Host = new HostString("192.168.1.20");
        remoteHttp.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.20");

        Assert.That(guard.AllowsCredentials(https), Is.True);
        Assert.That(guard.AllowsCredentials(loopback), Is.True);
        Assert.That(guard.AllowsCredentials(remoteHttp), Is.False);
    }
}

[TestFixture]
public sealed class ApplicationProxyCredentialsTests
{
    [Test]
    public void ReadTicket_usesTheHeaderAndIgnoresTheQueryString()
    {
        var credentials = new ApplicationProxyCredentials(new ApplicationProxyCookieProtector(new EphemeralDataProtectionProvider()));
        var header = new DefaultHttpContext();
        header.Request.Headers[ApplicationProxyConstants.TicketHeader] = "from-header";
        header.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["ticket"] = "from-query" });
        var queryOnly = new DefaultHttpContext();
        queryOnly.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["ticket"] = "from-query" });

        Assert.That(credentials.ReadTicket(header), Is.EqualTo("from-header"));
        Assert.That(credentials.ReadTicket(queryOnly), Is.Null);
    }
}

[TestFixture]
public sealed class ApplicationProxyBootstrapPageTests
{
    [Test]
    public async Task WriteAsync_emitsAFragmentTicketConsumer()
    {
        var page = new ApplicationProxyBootstrapPage();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await page.WriteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var html = await reader.ReadToEndAsync();

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(html, Does.Contain(ApplicationProxyConstants.TicketHeader));
        Assert.That(html, Does.Contain("#ticket="));
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
    public void IsForeignOrigin_blocksWritesFromADifferentPortOnTheSameHost()
    {
        var guard = new ApplicationProxyCsrfGuard();
        var context = WriteContext(HttpMethods.Post, "https://agent.example:8443");
        context.Request.Host = new HostString("agent.example");

        Assert.That(guard.IsForeignOrigin(context), Is.True);
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
        if (Uri.TryCreate(origin, UriKind.Absolute, out var parsed))
            context.Request.Scheme = parsed.Scheme;
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
        using var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:10100/");

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
        Assert.That(context.Response.Headers.SetCookie.ToString(), Does.Contain("sid=1"));
    }

    [Test]
    public async Task TransformResponseAsync_dropsReservedServerCookieNames()
    {
        var transformer = new ApplicationProxyRequestTransformer();
        var context = new DefaultHttpContext();
        using var proxyResponse = new HttpResponseMessage(HttpStatusCode.OK);
        proxyResponse.Headers.TryAddWithoutValidation("Set-Cookie", "agent-up-proxy=stolen; Path=/");
        proxyResponse.Headers.TryAddWithoutValidation("Set-Cookie", "agent-up-other=nope; Path=/");
        proxyResponse.Headers.TryAddWithoutValidation("Set-Cookie", "theme=dark; Path=/");

        await transformer.TransformResponseAsync(context, proxyResponse, CancellationToken.None);

        Assert.That(context.Response.Headers.SetCookie.ToString(), Does.Contain("theme=dark"));
        Assert.That(context.Response.Headers.SetCookie.ToString(), Does.Not.Contain("agent-up-"));
    }

    [Test]
    public async Task TransformRequestAsync_leavesRequestsWithoutCookiesUnchanged()
    {
        var transformer = new ApplicationProxyRequestTransformer();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/";
        using var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:10100/");

        await transformer.TransformRequestAsync(context, proxyRequest, "http://127.0.0.1:10100", CancellationToken.None);

        Assert.That(proxyRequest.Headers.Contains("Cookie"), Is.False);
    }

    [Test]
    public async Task TransformResponseAsync_keepsRelativeAndNonLoopbackLocations()
    {
        var transformer = new ApplicationProxyRequestTransformer();
        var missingPort = new DefaultHttpContext();
        missingPort.Request.Scheme = "https";
        missingPort.Request.Host = new HostString("agent.example");
        using var relative = new HttpResponseMessage(HttpStatusCode.Found);
        relative.Headers.Location = new Uri("/login", UriKind.Relative);
        var relativeContext = new DefaultHttpContext();
        relativeContext.Request.Scheme = "https";
        relativeContext.Request.Host = new HostString("agent.example");
        relativeContext.Items[ApplicationProxyConstants.DestinationPortItem] = 10100;
        var external = new DefaultHttpContext();
        external.Request.Scheme = "https";
        external.Request.Host = new HostString("agent.example");
        external.Items[ApplicationProxyConstants.DestinationPortItem] = 10100;
        using var externalResponse = new HttpResponseMessage(HttpStatusCode.Found);
        externalResponse.Headers.Location = new Uri("https://example.test/login");

        await transformer.TransformResponseAsync(missingPort, relative, CancellationToken.None);
        await transformer.TransformResponseAsync(relativeContext, relative, CancellationToken.None);
        await transformer.TransformResponseAsync(external, externalResponse, CancellationToken.None);
        await transformer.TransformResponseAsync(external, null, CancellationToken.None);

        Assert.That(missingPort.Response.Headers.Location.ToString(), Is.EqualTo("/login"));
        Assert.That(relativeContext.Response.Headers.Location.ToString(), Is.EqualTo("/login"));
        Assert.That(external.Response.Headers.Location.ToString(), Is.EqualTo("https://example.test/login"));
    }
}

[TestFixture]
public sealed class ApplicationHttpForwarderTests
{
    [Test]
    public async Task ForwardAsync_returnsWhenTheProxyCompletes()
    {
        var yarp = new FakeYarpHttpForwarder();
        using var forwarder = new ApplicationHttpForwarder(yarp, HttpTransformer.Default);
        var context = new DefaultHttpContext();

        await forwarder.ForwardAsync(context, 10100);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(context.Items[ApplicationProxyConstants.DestinationPortItem], Is.EqualTo(10100));
    }

    [Test]
    public async Task ForwardAsync_writesBadGatewayWhenTheForwarderReportsAnError()
    {
        var yarp = new FakeYarpHttpForwarder { Error = ForwarderError.RequestTimedOut };
        using var forwarder = new ApplicationHttpForwarder(yarp, HttpTransformer.Default);
        var context = new DefaultHttpContext();

        await forwarder.ForwardAsync(context, 10100);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status502BadGateway));
    }

    [Test]
    public async Task ForwardAsync_doesNotOverwriteAResponseThatAlreadyStarted()
    {
        var yarp = new FakeYarpHttpForwarder { Error = ForwarderError.RequestTimedOut, StartResponse = true };
        using var forwarder = new ApplicationHttpForwarder(yarp, HttpTransformer.Default);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await forwarder.ForwardAsync(context, 10100);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [TestCase(typeof(HttpRequestException))]
    [TestCase(typeof(SocketException))]
    [TestCase(typeof(IOException))]
    public async Task ForwardAsync_writesUnavailableWhenTheDestinationCannotBeReached(Type exceptionType)
    {
        var yarp = new FakeYarpHttpForwarder { Exception = (Exception)Activator.CreateInstance(exceptionType)! };
        using var forwarder = new ApplicationHttpForwarder(yarp, HttpTransformer.Default);
        var context = new DefaultHttpContext();

        await forwarder.ForwardAsync(context, 10100);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status502BadGateway));
        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
    }

    [Test]
    public async Task ForwardAsync_ignoresDestinationFailuresAfterTheResponseHasStarted()
    {
        var yarp = new FakeYarpHttpForwarder
        {
            Exception = new IOException("reset"),
            StartResponse = true
        };
        using var forwarder = new ApplicationHttpForwarder(yarp, HttpTransformer.Default);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await forwarder.ForwardAsync(context, 10100);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
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

file sealed class FormatThrowingProtection : IDataProtectionProvider, IDataProtector
{
    public IDataProtector CreateProtector(string purpose) => this;

    public byte[] Protect(byte[] plaintext) => plaintext;

    public byte[] Unprotect(byte[] protectedData) => throw new FormatException();
}

file sealed class FakeYarpHttpForwarder : IHttpForwarder
{
    public ForwarderError Error { get; set; } = ForwarderError.None;
    public Exception? Exception { get; set; }
    public bool StartResponse { get; set; }

    public ValueTask<ForwarderError> SendAsync(
        HttpContext context,
        string destinationPrefix,
        HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig,
        HttpTransformer transformer)
        => SendAsync(context, destinationPrefix, httpClient, requestConfig, transformer, CancellationToken.None);

    public ValueTask<ForwarderError> SendAsync(
        HttpContext context,
        string destinationPrefix,
        HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig,
        HttpTransformer transformer,
        CancellationToken cancellationToken)
    {
        if (StartResponse)
            context.Features.Set<IHttpResponseFeature>(new StartedHttpResponseFeature(context.Response));
        if (Exception is not null)
            throw Exception;
        return ValueTask.FromResult(Error);
    }
}

file sealed class StartedHttpResponseFeature : IHttpResponseFeature
{
    public StartedHttpResponseFeature(HttpResponse response)
    {
        StatusCode = response.StatusCode;
        Headers = response.Headers;
        Body = response.Body;
    }

    public int StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public IHeaderDictionary Headers { get; set; }
    public Stream Body { get; set; }
    public bool HasStarted => true;
    public void OnStarting(Func<object, Task> callback, object state) { }
    public void OnCompleted(Func<object, Task> callback, object state) { }
}
