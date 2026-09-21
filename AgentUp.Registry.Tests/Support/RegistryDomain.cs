using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Shared.Providers;

namespace AgentUp.Registry.Tests.Support;

internal static class RegistryDomain
{
    public const string DotnetId = "dotnet";
    public const string DotnetVersion = "1.0.0";
    public const string DotnetDisplayName = ".NET";
    public const string Publisher = "agent-up";
    public const string SchemaVersion = "1";
    public const string NixpkgsRev = "6d663c0533ff269008fb84e45930151e37c99db9";

    /// <summary>
    /// A registry root for a test whose subject never touches the filesystem, so that the path
    /// validator every storage provider now requires has one name across the project.
    /// </summary>
    public static string RegistryRoot => Path.Join(Path.GetTempPath(), "agent-up-registry");

    public static RegistryPathValidator Paths(string registryRoot) => new(registryRoot);

    public static CapabilityPackageManifest DotnetPackage()
        => new()
        {
            SchemaVersion = SchemaVersion,
            Id = DotnetId,
            Version = DotnetVersion,
            DisplayName = DotnetDisplayName,
            Publisher = Publisher,
            Kind = "runtime",
            Platforms = ["linux", "macos"],
            Nix = new CapabilityNixSpec
            {
                Nixpkgs = new CapabilityNixpkgsPin { Rev = NixpkgsRev },
                Packages = ["dotnet-sdk_10"]
            },
            Provides = ["dotnet"],
            Parameters = new Dictionary<string, CapabilityParameterSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["project"] = new() { Required = true, Type = "path" },
                ["sdk"] = new() { Type = "versionRange" }
            },
            Launch = new CapabilityLaunchTemplate
            {
                Command = "dotnet",
                Arguments = ["run", "--project", "{{parameters.project}}"]
            },
            Probe = new CapabilityProbeTemplate { Command = "dotnet", Arguments = ["--version"] }
        };

    public static CapabilityTemplateValues DotnetValues(string project = "Api.csproj")
        => new(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["project"] = project },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
