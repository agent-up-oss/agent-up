using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Registry.Tests.Support;

internal static class RegistryDomain
{
    public const string DotnetId = "dotnet";
    public const string DotnetVersion = "1.0.0";
    public const string DotnetDisplayName = ".NET";
    public const string Publisher = "agent-up";
    public const string SchemaVersion = "1";
    public const string NixpkgsRev = "b134951a4c9f3c995fd7be05f9a8dafa8c4ffb90";
    public const string NixpkgsSha256 = "sha256-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

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
                Nixpkgs = new CapabilityNixpkgsPin { Rev = NixpkgsRev, Sha256 = NixpkgsSha256 },
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
