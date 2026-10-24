using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using AgentUp.TestAgents.Features.Authentication.Providers;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Authentication.Provider;

// A single slow identity-provider poll must not abort a sign-in whose challenge is already
// printed. That is the failure the iOS device-code suite recorded as exit 130.
[TestFixture]
public sealed class PollingLoginFlowTests
{
    [Test]
    public async Task DeviceCode_retriesATimedOutTokenPollAndThenSignsIn()
    {
        using var output = new AgentOutput();
        using var client = Client(
            Grant(),
            HttpTimeout(),
            Token("test-oat-device"));

        var token = await new DeviceCodeLoginFlow(client, "http://idp.test")
            .RunAsync(output, TextReader.Null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.EqualTo("test-oat-device"));
            Assert.That(output.ToString(), Does.Contain("ABCD-1234"));
        });
    }

    [Test]
    public async Task DeviceCode_retriesADroppedTokenPollAndThenSignsIn()
    {
        using var output = new AgentOutput();
        using var client = Client(
            Grant(),
            Dropped(),
            Token("test-oat-device"));

        var token = await new DeviceCodeLoginFlow(client, "http://idp.test")
            .RunAsync(output, TextReader.Null, CancellationToken.None);

        Assert.That(token, Is.EqualTo("test-oat-device"));
    }

    [Test]
    public async Task DeviceCode_stillGivesUpWhenTheGrantExpires()
    {
        using var output = new AgentOutput();
        using var client = Client(
            Grant(),
            Json(HttpStatusCode.BadRequest, """{"error":"expired_token"}"""));

        var token = await new DeviceCodeLoginFlow(client, "http://idp.test")
            .RunAsync(output, TextReader.Null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Null);
            Assert.That(output.ToString(), Does.Contain("Sign-in failed: expired_token"));
        });
    }

    [Test]
    public async Task DeviceCode_rethrowsWhenTheCallerCancelsATokenPoll()
    {
        using var output = new AgentOutput();
        using var lifetime = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var polling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = Client(
            Grant(),
            WaitUntilCancelled(polling));

        var login = new DeviceCodeLoginFlow(client, "http://idp.test")
            .RunAsync(output, TextReader.Null, lifetime.Token);
        await output.WaitForLineContainingAsync("ABCD-1234", deadline.Token);
        await polling.Task.WaitAsync(deadline.Token);
        await lifetime.CancelAsync();

        Assert.That(async () => await login, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task SilentPoll_retriesATimedOutStatusPollAndThenSignsIn()
    {
        using var output = new AgentOutput();
        using var client = Client(
            Start(),
            HttpTimeout(),
            Approved("test-oat-poll"));

        var token = await new SilentPollLoginFlow(client, "http://idp.test")
            .RunAsync(output, TextReader.Null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.EqualTo("test-oat-poll"));
            Assert.That(output.ToString(), Does.Contain("/login/abc"));
        });
    }

    private static HttpClient Client(params Func<CancellationToken, Task<HttpResponseMessage>>[] steps) =>
        new(new SequencedHandler(steps));

    private static Func<CancellationToken, Task<HttpResponseMessage>> Grant() =>
        Json(HttpStatusCode.OK, """
            {"device_code":"dc-1","user_code":"ABCD-1234","verification_uri":"http://idp.test/device","expires_in":600,"interval":1}
            """);

    private static Func<CancellationToken, Task<HttpResponseMessage>> Token(string accessToken) =>
        Json(HttpStatusCode.OK, $$"""{"access_token":"{{accessToken}}","token_type":"Bearer"}""");

    private static Func<CancellationToken, Task<HttpResponseMessage>> Start() =>
        Json(HttpStatusCode.OK, """
            {"login_url":"http://idp.test/login/abc","status_url":"http://idp.test/login/status/abc"}
            """);

    private static Func<CancellationToken, Task<HttpResponseMessage>> Approved(string accessToken) =>
        Json(HttpStatusCode.OK, $$"""{"status":"approved","access_token":"{{accessToken}}"}""");

    private static Func<CancellationToken, Task<HttpResponseMessage>> HttpTimeout() =>
        _ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout elapsing.");

    private static Func<CancellationToken, Task<HttpResponseMessage>> Dropped() =>
        _ => throw new HttpRequestException("Connection reset.");

    private static Func<CancellationToken, Task<HttpResponseMessage>> WaitUntilCancelled(
        TaskCompletionSource polling) =>
        async cancellationToken =>
        {
            polling.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateResponse(HttpStatusCode.OK, "{}");
        };

    private static Func<CancellationToken, Task<HttpResponseMessage>> Json(HttpStatusCode status, string body) =>
        _ => Task.FromResult(CreateResponse(status, body));

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage CreateResponse(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class SequencedHandler(Func<CancellationToken, Task<HttpResponseMessage>>[] steps)
        : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_index >= steps.Length)
                throw new InvalidOperationException($"Unexpected request {_index}: {request.RequestUri}");
            return steps[_index++](cancellationToken);
        }
    }
}
