using AgentUp.Desktop.Composition;

namespace AgentUp.Desktop.Tests.Features.Composition.Provider;

[TestFixture]
public sealed class SentryTelemetryEnvironmentTests
{
    private string? _originalDsn;
    private string? _originalDotnetEnvironment;
    private string? _originalAspNetCoreEnvironment;

    [SetUp]
    public void SetUp()
    {
        _originalDsn = Environment.GetEnvironmentVariable(SentryTelemetry.DsnVariable);
        _originalDotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        _originalAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(SentryTelemetry.DsnVariable, _originalDsn);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _originalDotnetEnvironment);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _originalAspNetCoreEnvironment);
    }

    [Test]
    public void ReadProcessRequest_omitsDsnWhenUnset()
    {
        Environment.SetEnvironmentVariable(SentryTelemetry.DsnVariable, null);

        var request = SentryTelemetry.ReadProcessRequest(typeof(SentryTelemetry).Assembly);

        Assert.That(request.Dsn, Is.Null.Or.Empty);
        Assert.That(SentryTelemetry.TryCreate(request, out _), Is.False);
    }

    [Test]
    public void ReadProcessRequest_capturesDsnAndDevelopmentHostWhenPresent()
    {
        Environment.SetEnvironmentVariable(SentryTelemetry.DsnVariable, "https://public@sentry.example/1");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

        var request = SentryTelemetry.ReadProcessRequest(typeof(SentryTelemetry).Assembly);

        Assert.That(SentryTelemetry.TryCreate(request, out var settings), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(request.Dsn, Is.EqualTo("https://public@sentry.example/1"));
            Assert.That(settings!.Component, Is.EqualTo("desktop"));
            Assert.That(settings.Deployment, Is.EqualTo("development"));
            Assert.That(settings.Environment, Is.EqualTo("development"));
        });
    }
}
