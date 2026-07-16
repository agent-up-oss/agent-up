using System.Net;
using AgentUp.PackageSmoke.Features.Security;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Tests.Features.Security;

[TestFixture]
public class RuntimeSecurityChecksTests
{
    // -- Port binding -----------------------------------------------------------

    [Test]
    public async Task RunAsync_addsInfoFindingWhenAllListenersAreLoopback()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Loopback, 5000), new IPEndPoint(IPAddress.IPv6Loopback, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Null);
        Assert.That(BindingInfo(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenListenerIsOnAnyIpv4()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Any, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenListenerIsOnAnyIpv6()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.IPv6Any, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenLoopbackAndNonLoopbackBothPresent()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Loopback, 5000), new IPEndPoint(IPAddress.Any, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_ignoresListenersOnOtherPorts()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Any, 8080)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Null);
    }

    [Test]
    public async Task RunAsync_addsInfoFindingWhenNoListenersFoundOnPort()
    {
        var checks = MakeChecks(listeners: [], serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Null);
        Assert.That(BindingInfo(assert), Is.Not.Null);
    }

    // -- Server header ----------------------------------------------------------

    [Test]
    public async Task RunAsync_addsInfoFindingForCleanServerHeader()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Loopback, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(HeaderError(assert), Is.Null);
        Assert.That(HeaderInfo(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_addsInfoFindingWhenServerHeaderIsAbsent()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Loopback, 5000)],
            serverHeader: null);
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(HeaderError(assert), Is.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenServerHeaderContainsVersion()
    {
        var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Loopback, 5000)],
            serverHeader: "CustomServer/1.0");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(HeaderError(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenServerIsUnreachable()
    {
        var handler = new FakeHttpMessageHandler(
            _ => Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var checks = new RuntimeSecurityChecks(
            new FakeNetworkStateProvider(new IPEndPoint(IPAddress.Loopback, 5000)),
            new HttpClient(handler));
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(assert.Findings.FirstOrDefault(f => f.Code == "security.headers.probe" && f.Severity == FindingSeverity.Error), Is.Not.Null);
    }

    // -- Helpers ----------------------------------------------------------------

    private static RuntimeSecurityChecks MakeChecks(IPEndPoint[] listeners, string? serverHeader)
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            using var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (serverHeader is not null)
                response.Headers.TryAddWithoutValidation("Server", serverHeader);

            var result = new HttpResponseMessage(response.StatusCode);
            foreach (var header in response.Headers)
                result.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return Task.FromResult(result);
        });
        return new RuntimeSecurityChecks(new FakeNetworkStateProvider(listeners), new HttpClient(handler));
    }

    private static SmokeFinding? BindingError(FileAssertions assert)
        => assert.Findings.FirstOrDefault(f => f.Code == "security.binding.loopback" && f.Severity == FindingSeverity.Error);

    private static SmokeFinding? BindingInfo(FileAssertions assert)
        => assert.Findings.FirstOrDefault(f => f.Code == "security.binding.loopback" && f.Severity == FindingSeverity.Info);

    private static SmokeFinding? HeaderError(FileAssertions assert)
        => assert.Findings.FirstOrDefault(f => f.Code == "security.headers.server" && f.Severity == FindingSeverity.Error);

    private static SmokeFinding? HeaderInfo(FileAssertions assert)
        => assert.Findings.FirstOrDefault(f => f.Code == "security.headers.server" && f.Severity == FindingSeverity.Info);
}
