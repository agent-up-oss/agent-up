using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

public sealed class DotnetCapabilityAdapter(IDotnetVersionProvider versions) : ICapabilityAdapter
{
    public CapabilityDescriptor Descriptor { get; } =
        new("dotnet", ".NET", "1.0.0", true, ["linux", "macos", "windows"]);

    public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
        versions.DiscoverAsync(cancellationToken);

    public Task<CapabilityValidationResult> ValidateAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken)
    {
        var messages = new List<CapabilityValidationMessage>();
        if (!declaration.Parameters.TryGetValue("project", out var project) || string.IsNullOrWhiteSpace(project))
        {
            messages.Add(new CapabilityValidationMessage("dotnet.project.required", "A .NET capability declaration requires a project path.", CapabilityValidationSeverity.Error));
        }

        if (declaration.Requirements.TryGetValue("sdk", out var sdkRange) && !MatchesAnyInstalledSdk(installedVersions, sdkRange))
        {
            messages.Add(new CapabilityValidationMessage(
                "dotnet.sdk.missing",
                $"No installed .NET SDK matches '{sdkRange}'.",
                CapabilityValidationSeverity.Error));
        }

        var result = messages.Any(message => message.Severity == CapabilityValidationSeverity.Error)
            ? CapabilityValidationResult.Failure([.. messages])
            : CapabilityValidationResult.Success([.. messages]);

        return Task.FromResult(result);
    }

    public Task<CapabilityLaunchPlan> CreateLaunchPlanAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken)
    {
        var project = declaration.Parameters["project"];
        var arguments = declaration.Parameters.TryGetValue("arguments", out var extra) ? " " + extra : "";
        var command = $"dotnet run --project {Quote(project)}{arguments}";
        return Task.FromResult(new CapabilityLaunchPlan(command));
    }

    private static bool MatchesAnyInstalledSdk(IReadOnlyList<CapabilityInstalledVersion> installedVersions, string range)
    {
        if (string.IsNullOrWhiteSpace(range))
            return installedVersions.Count > 0;

        if (range.EndsWith(".x", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = range[..^1];
            return installedVersions.Any(version => version.Version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return installedVersions.Any(version => string.Equals(version.Version, range, StringComparison.OrdinalIgnoreCase));
    }

    private static string Quote(string value) =>
        value.Contains(' ') ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"" : value;
}
