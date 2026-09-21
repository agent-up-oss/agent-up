using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Controllers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Services;
using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.LocalStore.Services;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Providers;
using AgentUp.Server.Features.Capabilities.Services;

namespace AgentUp.Server.Tests.Support;

internal static class CapabilityModuleHarness
{
    public static CapabilityModuleService CreateService(
        bool nixAvailable = true,
        bool enabled = false,
        CapabilityPackageManifest? manifest = null,
        ICapabilityModuleLoader? loader = null)
    {
        manifest ??= DotnetManifest();
        var store = new MemoryRegistryStore(manifest);
        var enabledSet = new MemoryEnabledSet(enabled ? [new CapabilityPackageRef(manifest.Id, manifest.Version)] : []);
        return new CapabilityModuleService(
            new LocalRegistryController(new LocalRegistryService(
                store,
                new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer()))),
            enabledSet,
            new FixedNixPresence(nixAvailable),
            new NixCapabilityEnvironmentController(
                new NixCapabilityEnvironmentService(new NixCapabilityCommandBuilder(), new CapabilityIndexMergeProvider())),
            new CapabilityRuntimePathProvider(),
            loader ?? new CapabilityModuleLoadProvider());
    }

    public static CapabilityPackageManifest DotnetManifest()
        => new()
        {
            SchemaVersion = "1",
            Id = "dotnet",
            Version = "1.0.0",
            DisplayName = ".NET",
            Publisher = "agent-up",
            Kind = "runtime",
            Provides = ["dotnet"],
            Launch = new CapabilityLaunchTemplate
            {
                Command = "dotnet",
                Arguments = ["run", "--project", "{{parameters.project}}"]
            },
            Parameters = new Dictionary<string, CapabilityParameterSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["project"] = new() { Required = true, Type = "path" }
            }
        };

    internal sealed class MemoryEnabledSet : ICapabilityEnabledSetStore
    {
        private IReadOnlyList<CapabilityPackageRef> _modules;

        public MemoryEnabledSet(IReadOnlyList<CapabilityPackageRef> modules) => _modules = modules;

        public EnabledCapabilitySetDto Read() => new() { Modules = _modules };

        public void Write(EnabledCapabilitySetDto set) => _modules = set.Modules;
    }

    internal sealed class FixedNixPresence(bool available) : INixPresenceProvider
    {
        public bool IsAvailable { get; } = available;
    }

    private sealed class MemoryRegistryStore(CapabilityPackageManifest manifest) : ILocalRegistryDirectoryStore
    {
        public CapabilityRegistryIndex ReadIndex()
            => new()
            {
                Packages =
                [
                    new CapabilityRegistryIndexEntry(
                        manifest.Id, manifest.Version, manifest.DisplayName, manifest.Publisher, manifest.Kind)
                ]
            };

        public LocalRegistryPackageDto? ReadPackage(string id, string version)
            => manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
               && manifest.Version.Equals(version, StringComparison.Ordinal)
                ? Package()
                : null;

        public LocalRegistryPackageDto ReadStagedPackage(string packageDirectory) => Package();

        public IReadOnlyList<LocalRegistryPackageDto> ReadPackages() => [Package()];

        public void WritePackage(string packageDirectory)
        {
        }

        private LocalRegistryPackageDto Package()
            => new(manifest, "/packages/" + manifest.Id + "/" + manifest.Version, true);
    }
}
