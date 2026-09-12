using AgentUp.CLI.Composition;
using Sentry;

namespace AgentUp.CLI.Tests.Features.Composition.Unit;

[TestFixture]
public sealed class SentryTelemetryTests
{
    private const string SampleDsn = "https://public@sentry.example/1";

    [Test]
    public void TryCreate_returnsFalseWhenDsnMissing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SentryTelemetry.TryCreate(Request(dsn: null), out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(SentryTelemetry.TryCreate(Request(dsn: "  "), out var blank), Is.False);
            Assert.That(blank, Is.Null);
        });
    }

    [Test]
    public void ResolveDsn_prefersEnvironmentOverCompiledMetadata()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SentryTelemetry.ResolveDsn(null, null), Is.Null);
            Assert.That(SentryTelemetry.ResolveDsn("  ", SampleDsn), Is.EqualTo(SampleDsn));
            Assert.That(SentryTelemetry.ResolveDsn($" {SampleDsn} ", "https://other@sentry.example/2"), Is.EqualTo(SampleDsn));
        });
    }

    [Test]
    public void TryCreate_usesPackagedProductionContractWhenDsnPresent()
    {
        var created = SentryTelemetry.TryCreate(
            Request(hostEnvironmentName: "Production", runtimeIdentifier: "linux-x64"),
            out var settings);

        Assert.That(created, Is.True);
        Assert.That(settings, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(settings!.Dsn, Is.EqualTo(SampleDsn));
            Assert.That(settings.Component, Is.EqualTo("cli"));
            Assert.That(settings.Deployment, Is.EqualTo("packaged"));
            Assert.That(settings.Environment, Is.EqualTo("production"));
            Assert.That(settings.Release, Is.EqualTo("1.2.3"));
            Assert.That(settings.Rid, Is.EqualTo("linux-x64"));
        });
    }

    [Test]
    public void TryCreate_usesDevelopmentContractForDevelopmentHost()
    {
        var created = SentryTelemetry.TryCreate(
            Request(hostEnvironmentName: "Development"),
            out var settings);

        Assert.That(created, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(settings!.Deployment, Is.EqualTo("development"));
            Assert.That(settings.Environment, Is.EqualTo("development"));
        });
    }

    [Test]
    public void Apply_setsErrorOnlyOptionsAndTagReleaseContract()
    {
        var created = SentryTelemetry.TryCreate(
            Request(runtimeIdentifier: "linux-x64"),
            out var settings);
        Assert.That(created, Is.True);

        var options = new SentryOptions();
        SentryTelemetry.Apply(options, settings!);

        Assert.Multiple(() =>
        {
            Assert.That(options.Dsn, Is.EqualTo(SampleDsn));
            Assert.That(options.Release, Is.EqualTo("1.2.3"));
            Assert.That(options.Environment, Is.EqualTo("production"));
            Assert.That(options.SendDefaultPii, Is.False);
            Assert.That(options.TracesSampleRate, Is.EqualTo(0));
            Assert.That(options.IsGlobalModeEnabled, Is.True);
            Assert.That(options.DefaultTags["agentup.component"], Is.EqualTo("cli"));
            Assert.That(options.DefaultTags["agentup.deployment"], Is.EqualTo("packaged"));
            Assert.That(options.DefaultTags["agentup.rid"], Is.EqualTo("linux-x64"));
        });
    }

    [Test]
    public void ScrubSensitiveHeaders_removesAuthorizationAndCookieHeaders()
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.Request.Headers["Authorization"] = "Bearer secret";
        sentryEvent.Request.Headers["Cookie"] = "session=abc";
        sentryEvent.Request.Headers["Content-Type"] = "application/json";
        sentryEvent.Request.Cookies = "session=abc";

        var scrubbed = SentryTelemetry.ScrubSensitiveHeaders(sentryEvent);

        Assert.That(scrubbed, Is.SameAs(sentryEvent));
        Assert.Multiple(() =>
        {
            Assert.That(sentryEvent.Request.Headers.ContainsKey("Authorization"), Is.False);
            Assert.That(sentryEvent.Request.Headers.ContainsKey("Cookie"), Is.False);
            Assert.That(sentryEvent.Request.Headers["Content-Type"], Is.EqualTo("application/json"));
            Assert.That(sentryEvent.Request.Cookies, Is.Null);
        });
    }

    private static SentryTelemetryRequest Request(
        string? dsn = SampleDsn,
        string? kubernetesServiceHost = null,
        string? hostEnvironmentName = "Production",
        string? sentryEnvironment = null,
        string? sentryRelease = null,
        string? informationalVersion = "1.2.3",
        string? runtimeIdentifier = "linux-x64",
        bool runningInContainer = false)
        => new(
            dsn,
            kubernetesServiceHost,
            hostEnvironmentName,
            sentryEnvironment,
            sentryRelease,
            informationalVersion,
            runtimeIdentifier,
            runningInContainer);
}
