using System.Reflection;
using AgentUp.Server.Composition;

namespace AgentUp.Server.Tests.Features.Composition.Provider;

[TestFixture]
public sealed class SentryTelemetryEnvironmentTests
{
    private string? _originalDsn;
    private string? _originalKubernetes;
    private string? _originalSentryEnvironment;
    private string? _originalSentryRelease;
    private string? _originalRunningInContainer;

    [SetUp]
    public void SetUp()
    {
        _originalDsn = Environment.GetEnvironmentVariable(SentryTelemetry.DsnVariable);
        _originalKubernetes = Environment.GetEnvironmentVariable(SentryTelemetry.KubernetesServiceHostVariable);
        _originalSentryEnvironment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT");
        _originalSentryRelease = Environment.GetEnvironmentVariable("SENTRY_RELEASE");
        _originalRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(SentryTelemetry.DsnVariable, _originalDsn);
        Environment.SetEnvironmentVariable(SentryTelemetry.KubernetesServiceHostVariable, _originalKubernetes);
        Environment.SetEnvironmentVariable("SENTRY_ENVIRONMENT", _originalSentryEnvironment);
        Environment.SetEnvironmentVariable("SENTRY_RELEASE", _originalSentryRelease);
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", _originalRunningInContainer);
    }

    [Test]
    public void ReadProcessRequest_omitsDsnWhenUnset()
    {
        Environment.SetEnvironmentVariable(SentryTelemetry.DsnVariable, null);

        var request = SentryTelemetry.ReadProcessRequest("Production", typeof(SentryTelemetry).Assembly);

        Assert.That(request.Dsn, Is.Null.Or.Empty);
        Assert.That(SentryTelemetry.TryCreate(request, out _), Is.False);
    }

    [Test]
    public void ReadProcessRequest_capturesDsnAndClusterSignalsWhenPresent()
    {
        Environment.SetEnvironmentVariable(SentryTelemetry.DsnVariable, "https://public@sentry.example/1");
        Environment.SetEnvironmentVariable(SentryTelemetry.KubernetesServiceHostVariable, "10.0.0.1");
        Environment.SetEnvironmentVariable("SENTRY_ENVIRONMENT", "production");
        Environment.SetEnvironmentVariable("SENTRY_RELEASE", "9.9.9");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

        var request = SentryTelemetry.ReadProcessRequest("Production", Assembly.GetExecutingAssembly());

        Assert.That(SentryTelemetry.TryCreate(request, out var settings), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(request.Dsn, Is.EqualTo("https://public@sentry.example/1"));
            Assert.That(settings!.Deployment, Is.EqualTo("helm"));
            Assert.That(settings.Environment, Is.EqualTo("production"));
            Assert.That(settings.Release, Is.EqualTo("9.9.9"));
            Assert.That(settings.Rid, Is.EqualTo("docker"));
            Assert.That(settings.Component, Is.EqualTo("server"));
        });
    }
}
