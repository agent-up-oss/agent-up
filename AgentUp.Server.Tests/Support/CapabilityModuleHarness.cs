using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Controllers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Providers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Services;
using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Providers;
using AgentUp.Server.Features.Capabilities.Services;

namespace AgentUp.Server.Tests.Support;

internal static class CapabilityModuleHarness
{
    /// <param name="modules">The Server's built-in capabilities; a .NET runtime when omitted.</param>
    /// <param name="enabled">Whether every module starts enabled.</param>
    /// <param name="onPath">Executables the software probe finds on the search path.</param>
    public static CapabilityModuleService CreateService(
        bool nixAvailable = true,
        bool enabled = false,
        IReadOnlyList<ICapability>? modules = null,
        IReadOnlyList<string>? onPath = null,
        MemorySettings? settings = null)
    {
        modules ??= [Dotnet()];
        var nix = new FixedNixPresence(nixAvailable);
        return new CapabilityModuleService(
            new FixedCatalog(modules),
            new MemoryEnabledSet(enabled
                ? modules.Select(module => new CapabilityPackageRef(module.Identity.Id, module.Identity.PackageVersion)).ToArray()
                : []),
            settings ?? new MemorySettings(),
            new FixedSoftwareProbe(nix, onPath ?? []),
            nix,
            new MemoryShells(),
            new NixCapabilityEnvironmentController(
                new NixCapabilityEnvironmentService(new NixCapabilityCommandBuilder(), new CapabilityIndexMergeProvider())),
            new CapabilityRuntimePathProvider(null));
    }

    public static StubRuntimeCapability Dotnet() => new()
    {
        Identity = new CapabilityIdentity("dotnet", "1.0.0", ".NET", "agent-up"),
        Software = [new CapabilitySoftware("dotnet", DeliveredByNix: true)],
        NixPackages = [new NixPackageDeclaration("dotnet-sdk_10")],
        Provides = ["dotnet"],
        ExtraAttributes = [new Sdk.Runtime.RuntimeAttributeSpec("project", true, "path")]
    };

    internal sealed class FixedCatalog(IReadOnlyList<ICapability> modules) : ICapabilityCatalog
    {
        public IReadOnlyList<ICapability> Modules { get; } = modules;
    }

    internal sealed class MemoryEnabledSet : ICapabilityEnabledSetStore
    {
        private IReadOnlyList<CapabilityPackageRef> _modules;

        public MemoryEnabledSet(IReadOnlyList<CapabilityPackageRef> modules) => _modules = modules;

        public EnabledCapabilitySetDto Read() => new() { Modules = _modules };

        public void Write(EnabledCapabilitySetDto set) => _modules = set.Modules;
    }

    internal sealed class MemorySettings : ICapabilitySettingsStore
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Read(string capabilityId)
            => _values.GetValueOrDefault(capabilityId) ?? new Dictionary<string, string>();

        public void Write(string capabilityId, IReadOnlyDictionary<string, string> values)
            => _values[capabilityId] = new Dictionary<string, string>(values);
    }

    internal sealed class FixedNixPresence(bool available) : INixPresenceProvider
    {
        public bool IsAvailable { get; } = available;
    }

    /// <summary>The real probe's rules without a filesystem: Nix for what Nix delivers, a fixed search path for the rest.</summary>
    internal sealed class FixedSoftwareProbe(INixPresenceProvider nix, IReadOnlyList<string> onPath) : ICapabilitySoftwareProbe
    {
        public IReadOnlyList<string> Missing(IReadOnlyList<CapabilitySoftware> software)
            => software
                .Where(item => item.DeliveredByNix ? !nix.IsAvailable : !onPath.Contains(item.Executable))
                .Select(item => item.DeliveredByNix ? CapabilitySoftwareProbe.Nix : item.Executable)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    internal sealed class MemoryShells : ICapabilityNixShellProvider
    {
        public List<(string Id, IReadOnlyList<string> Packages)> Written { get; } = [];

        public string ShellPath(string capabilityId, IReadOnlyList<string> packages)
        {
            Written.Add((capabilityId, packages));
            return "/data/capabilities/nix/" + capabilityId + "/default.nix";
        }
    }
}
