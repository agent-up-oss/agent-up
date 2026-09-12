using AgentUp.Server.Composition;
using Sentry;
using Sentry.AspNetCore;

namespace AgentUp.Server.Tests.Features.Composition.Unit;

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
            Assert.That(settings.Component, Is.EqualTo("server"));
            Assert.That(settings.Deployment, Is.EqualTo("packaged"));
            Assert.That(settings.Environment, Is.EqualTo("production"));
            Assert.That(settings.Release, Is.EqualTo("1.2.3"));
            Assert.That(settings.Rid, Is.EqualTo("linux-x64"));
        });
    }

    [Test]
    public void TryCreate_usesHelmProductionAndDockerRidInCluster()
    {
        var created = SentryTelemetry.TryCreate(
            Request(kubernetesServiceHost: "10.0.0.1", runningInContainer: true, runtimeIdentifier: "linux-x64"),
            out var settings);

        Assert.That(created, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(settings!.Deployment, Is.EqualTo("helm"));
            Assert.That(settings.Environment, Is.EqualTo("production"));
            Assert.That(settings.Rid, Is.EqualTo("docker"));
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
    public void TryCreate_honorsSentryReleaseAndEnvironmentOverrides()
    {
        var created = SentryTelemetry.TryCreate(
            Request(sentryEnvironment: "production", sentryRelease: "9.9.9", informationalVersion: "1.2.3"),
            out var settings);

        Assert.That(created, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(settings!.Environment, Is.EqualTo("production"));
            Assert.That(settings.Release, Is.EqualTo("9.9.9"));
        });
    }

    [Test]
    public void Apply_setsErrorOnlyOptionsAndTagReleaseContract()
    {
        var created = SentryTelemetry.TryCreate(
            Request(runtimeIdentifier: "linux-x64"),
            out var settings);
        Assert.That(created, Is.True);

        var options = new SentryAspNetCoreOptions();
        SentryTelemetry.Apply(options, settings!);

        Assert.Multiple(() =>
        {
            Assert.That(options.Dsn, Is.EqualTo(SampleDsn));
            Assert.That(options.Release, Is.EqualTo("1.2.3"));
            Assert.That(options.Environment, Is.EqualTo("production"));
            Assert.That(options.SendDefaultPii, Is.False);
            Assert.That(options.TracesSampleRate, Is.EqualTo(0));
            Assert.That(options.MinimumEventLevel, Is.EqualTo(Microsoft.Extensions.Logging.LogLevel.Error));
            Assert.That(options.MinimumBreadcrumbLevel, Is.EqualTo(Microsoft.Extensions.Logging.LogLevel.Warning));
            Assert.That(options.DefaultTags["agentup.component"], Is.EqualTo("server"));
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
        sentryEvent.Request.Headers["Set-Cookie"] = "session=abc";
        sentryEvent.Request.Headers["Content-Type"] = "application/json";
        sentryEvent.Request.Cookies = "session=abc";

        var scrubbed = SentryTelemetry.ScrubSensitiveHeaders(sentryEvent);

        Assert.That(scrubbed, Is.SameAs(sentryEvent));
        Assert.Multiple(() =>
        {
            Assert.That(sentryEvent.Request.Headers.ContainsKey("Authorization"), Is.False);
            Assert.That(sentryEvent.Request.Headers.ContainsKey("Cookie"), Is.False);
            Assert.That(sentryEvent.Request.Headers.ContainsKey("Set-Cookie"), Is.False);
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
