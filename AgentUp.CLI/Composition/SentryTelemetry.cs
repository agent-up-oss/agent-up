using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Sentry;

namespace AgentUp.CLI.Composition;

internal sealed record SentryTelemetryRequest(
    string? Dsn,
    string? KubernetesServiceHost,
    string? HostEnvironmentName,
    string? SentryEnvironment,
    string? SentryRelease,
    string? InformationalVersion,
    string? RuntimeIdentifier,
    bool RunningInContainer);

internal sealed record SentryTelemetrySettings(
    string Dsn,
    string Component,
    string Deployment,
    string Environment,
    string Release,
    string? Rid);

internal static class SentryTelemetry
{
    internal const string Component = "cli";
    internal const string DsnVariable = "SENTRY_DSN";
    internal const string CompiledDsnMetadataKey = "SentryDsn";
    internal const string KubernetesServiceHostVariable = "KUBERNETES_SERVICE_HOST";
    internal const string TagComponent = "agentup.component";
    internal const string TagDeployment = "agentup.deployment";
    internal const string TagRid = "agentup.rid";
    internal const string DeploymentHelm = "helm";
    internal const string DeploymentPackaged = "packaged";
    internal const string DeploymentDevelopment = "development";
    internal const string EnvironmentDevelopment = "development";
    internal const string EnvironmentProduction = "production";

    internal static IDisposable? Initialize()
    {
        var request = ReadProcessRequest(typeof(SentryTelemetry).Assembly);
        if (!TryCreate(request, out var settings))
            return null;

        return SentrySdk.Init(options => Apply(options, settings));
    }

    internal static SentryTelemetryRequest ReadProcessRequest(Assembly assembly)
        => new(
            Dsn: ResolveDsn(
                Environment.GetEnvironmentVariable(DsnVariable),
                ReadCompiledDsn(assembly)),
            KubernetesServiceHost: Environment.GetEnvironmentVariable(KubernetesServiceHostVariable),
            HostEnvironmentName: Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            SentryEnvironment: Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT"),
            SentryRelease: Environment.GetEnvironmentVariable("SENTRY_RELEASE"),
            InformationalVersion: assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            RuntimeIdentifier: RuntimeInformation.RuntimeIdentifier,
            RunningInContainer: string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                "true",
                StringComparison.OrdinalIgnoreCase));

    internal static string? ResolveDsn(string? environmentDsn, string? compiledDsn)
    {
        if (!string.IsNullOrWhiteSpace(environmentDsn))
            return environmentDsn.Trim();

        if (!string.IsNullOrWhiteSpace(compiledDsn))
            return compiledDsn.Trim();

        return null;
    }

    private static string? ReadCompiledDsn(Assembly assembly)
        => assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == CompiledDsnMetadataKey)
            ?.Value;

    internal static bool TryCreate(
        SentryTelemetryRequest request,
        [NotNullWhen(true)] out SentryTelemetrySettings? settings)
    {
        if (string.IsNullOrWhiteSpace(request.Dsn))
        {
            settings = null;
            return false;
        }

        var deployment = ResolveDeployment(request);
        settings = new SentryTelemetrySettings(
            Dsn: request.Dsn.Trim(),
            Component: Component,
            Deployment: deployment,
            Environment: ResolveEnvironment(request, deployment),
            Release: ResolveRelease(request),
            Rid: ResolveRid(request));
        return true;
    }

    internal static void Apply(SentryOptions options, SentryTelemetrySettings settings)
    {
        options.Dsn = settings.Dsn;
        options.Release = settings.Release;
        options.Environment = settings.Environment;
        options.SendDefaultPii = false;
        options.TracesSampleRate = 0;
        options.CaptureFailedRequests = false;
        options.AutoSessionTracking = false;
        options.IsGlobalModeEnabled = true;
        options.DefaultTags[TagComponent] = settings.Component;
        options.DefaultTags[TagDeployment] = settings.Deployment;
        if (!string.IsNullOrWhiteSpace(settings.Rid))
            options.DefaultTags[TagRid] = settings.Rid;

        options.SetBeforeSend(ScrubSensitiveHeaders);
        options.SetBeforeBreadcrumb(KeepWarningBreadcrumbs);
    }

    internal static SentryEvent? ScrubSensitiveHeaders(SentryEvent sentryEvent)
    {
        var headers = sentryEvent.Request.Headers;
        headers.Remove("Authorization");
        headers.Remove("Cookie");
        headers.Remove("Set-Cookie");
        sentryEvent.Request.Cookies = null;
        return sentryEvent;
    }

    internal static Breadcrumb? KeepWarningBreadcrumbs(Breadcrumb breadcrumb)
        => breadcrumb.Level < BreadcrumbLevel.Warning ? null : breadcrumb;

    private static string ResolveDeployment(SentryTelemetryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.KubernetesServiceHost))
            return DeploymentHelm;

        if (string.Equals(request.HostEnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            return DeploymentDevelopment;

        return DeploymentPackaged;
    }

    private static string ResolveEnvironment(SentryTelemetryRequest request, string deployment)
    {
        if (!string.IsNullOrWhiteSpace(request.SentryEnvironment))
            return request.SentryEnvironment.Trim();

        return deployment == DeploymentDevelopment ? EnvironmentDevelopment : EnvironmentProduction;
    }

    private static string ResolveRelease(SentryTelemetryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SentryRelease))
            return request.SentryRelease.Trim();

        if (!string.IsNullOrWhiteSpace(request.InformationalVersion))
            return request.InformationalVersion.Trim();

        return "0.0.0";
    }

    private static string? ResolveRid(SentryTelemetryRequest request)
    {
        if (request.RunningInContainer)
            return "docker";

        if (string.IsNullOrWhiteSpace(request.RuntimeIdentifier))
            return null;

        return request.RuntimeIdentifier.Trim();
    }
}
