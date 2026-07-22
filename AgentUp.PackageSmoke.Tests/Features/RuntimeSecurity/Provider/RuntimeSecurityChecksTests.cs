using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using System.Net;
using AgentUp.PackageSmoke.Features.RuntimeSecurity;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;
using AgentUp.PackageSmoke.Features.PackageValidation;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;
using AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity.Fake;

namespace AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity.Provider;

[TestFixture]
public class RuntimeSecurityChecksTests
{
    // -- Port binding -----------------------------------------------------------

    [Test]
    public async Task RunAsync_addsInfoFindingWhenAllListenersAreLoopback()
    {
        using var checks = MakeChecks(
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
        using var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Any, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenListenerIsOnAnyIpv6()
    {
        using var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.IPv6Any, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenLoopbackAndNonLoopbackBothPresent()
    {
        using var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Loopback, 5000), new IPEndPoint(IPAddress.Any, 5000)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_ignoresListenersOnOtherPorts()
    {
        using var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Any, 8080)],
            serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Null);
    }

    [Test]
    public async Task RunAsync_addsInfoFindingWhenNoListenersFoundOnPort()
    {
        using var checks = MakeChecks(listeners: [], serverHeader: "Kestrel");
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(BindingError(assert), Is.Null);
        Assert.That(BindingInfo(assert), Is.Not.Null);
    }

    // -- Server header ----------------------------------------------------------

    [Test]
    public async Task RunAsync_addsInfoFindingForCleanServerHeader()
    {
        using var checks = MakeChecks(
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
        using var checks = MakeChecks(
            listeners: [new IPEndPoint(IPAddress.Loopback, 5000)],
            serverHeader: null);
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(HeaderError(assert), Is.Null);
    }

    [Test]
    public async Task RunAsync_addsErrorFindingWhenServerHeaderContainsVersion()
    {
        using var checks = MakeChecks(
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
        using var checks = new RuntimeSecurityChecks(
            new FakeNetworkStateProvider(new IPEndPoint(IPAddress.Loopback, 5000)),
            new HttpClient(handler));
        var assert = new FileAssertions();

        await checks.RunAsync("http://127.0.0.1:5000", assert);

        Assert.That(assert.Findings.FirstOrDefault(f => f.Code == "security.headers.probe" && f.Severity == FindingSeverity.Error), Is.Not.Null);
    }

    // -- Helpers ----------------------------------------------------------------

    private static RuntimeSecurityChecks MakeChecks(IPEndPoint[] listeners, string? serverHeader)
    {
        var handler = new FakeHttpMessageHandler(_ => ResponseAsync(serverHeader));
        return new RuntimeSecurityChecks(new FakeNetworkStateProvider(listeners), new HttpClient(handler));
    }

    private static Task<HttpResponseMessage> ResponseAsync(string? serverHeader)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (serverHeader is not null)
            response.Headers.TryAddWithoutValidation("Server", serverHeader);
        return Task.FromResult(response);
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
