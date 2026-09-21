using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Controllers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Shared.Interfaces;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Features.Capabilities.Services;

public sealed class CapabilityModuleService(
    LocalRegistryController local,
    ICapabilityEnabledSetStore enabled,
    INixPresenceProvider nix,
    NixCapabilityEnvironmentController wrap,
    CapabilityRuntimePathProvider runtime,
    ICapabilityModuleLoader loader,
    IRegistryPathValidator? paths = null) : IEnabledCapabilityPackages
{
    public IReadOnlyList<CapabilityModuleDto> List()
    {
        var set = enabled.Read();
        return local.ListPackages().Select(package => ToDto(package, set)).ToArray();
    }

    public CapabilityPackageManifest? GetEnabled(string id)
    {
        var set = enabled.Read();
        var selected = set.Modules.FirstOrDefault(module => module.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
            return null;
        return local.Get(selected.Id, selected.Version)?.Manifest;
    }

    public CapabilityModuleDto Enable(string id, string? version)
    {
        var package = ResolvePackage(id, version);
        var set = enabled.Read();
        var modules = set.Modules
            .Where(module => !module.Id.Equals(package.Manifest.Id, StringComparison.OrdinalIgnoreCase))
            .Append(new CapabilityPackageRef(package.Manifest.Id, package.Manifest.Version))
            .ToArray();
        enabled.Write(new EnabledCapabilitySetDto { Modules = modules });
        return ToDto(package, enabled.Read());
    }

    public CapabilityModuleDto Disable(string id)
    {
        var set = enabled.Read();
        enabled.Write(new EnabledCapabilitySetDto
        {
            Modules = set.Modules.Where(module => !module.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToArray()
        });
        var package = local.ListPackages().FirstOrDefault(item => item.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Capability '{id}' is not in the local registry.");
        return ToDto(package, enabled.Read());
    }

    public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
    {
        var environments = EnabledPackages()
            .Where(package => package.Manifest.Kind.Equals("runtime", StringComparison.Ordinal)
                              && ProvidesCommand(package.Manifest, fileName))
            .Select(ToEnvironment)
            .ToArray();
        if (environments.Length == 0 || !nix.IsAvailable)
            return new CapabilityLaunchWrapDto(fileName, arguments);

        var merged = wrap.Merge(environments);
        var wrapped = wrap.Wrap(fileName, arguments, merged);
        return new CapabilityLaunchWrapDto(wrapped.FileName, wrapped.Arguments);
    }

    public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
    {
        var package = EnabledPackages()
            .FirstOrDefault(item => item.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (package is null || !nix.IsAvailable)
            return new CapabilityLaunchWrapDto(fileName, arguments);

        var wrapped = wrap.Wrap(fileName, arguments, ToEnvironment(package));
        return new CapabilityLaunchWrapDto(wrapped.FileName, wrapped.Arguments);
    }

    public IRuntimeCapability? GetRuntime(string id)
        => Load(id)?.Runtime;

    public IReadOnlyList<IRuntimeCapability> ListRuntimes()
        => EnabledPackages()
            .Where(package => package.Manifest.Kind.Equals("runtime", StringComparison.Ordinal))
            .Select(package => Load(package.Manifest.Id)?.Runtime)
            .Where(runtime => runtime is not null)
            .Cast<IRuntimeCapability>()
            .ToArray();

    public IAgentCapability? GetAgent(string id)
        => Load(id)?.Agent;

    public IReadOnlyList<IAgentCapability> ListAgents()
        => EnabledPackages()
            .Where(package => package.Manifest.Kind.Equals("agent", StringComparison.Ordinal))
            .Select(package => Load(package.Manifest.Id)?.Agent)
            .Where(agent => agent is not null)
            .Cast<IAgentCapability>()
            .ToArray();

    public CapabilityLaunchPlan? AgentLaunch(string id)
    {
        var agent = GetAgent(id);
        if (agent is not null)
        {
            var launched = agent.Launch();
            return ToPlan(id, launched.FileName, launched.Arguments);
        }

        var package = EnabledPackages()
            .FirstOrDefault(item => item.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        var launch = package?.Manifest.Launch;
        if (package is null || launch is null || string.IsNullOrWhiteSpace(launch.Command))
            return null;
        return ToPlan(id, launch.Command, launch.Arguments ?? []);
    }

    private CapabilityLaunchPlan? ToPlan(string id, string command, IReadOnlyList<string> arguments)
    {
        var package = EnabledPackages()
            .FirstOrDefault(item => item.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (package is null)
            return null;
        if (!nix.IsAvailable)
            return new CapabilityLaunchPlan(command, Arguments: arguments);

        var environments = new List<NixEnvironmentSpec> { ToEnvironment(package) };
        environments.AddRange(
            EnabledPackages()
                .Where(item => item.Manifest.Kind.Equals("runtime", StringComparison.Ordinal)
                               && !item.Manifest.Id.Equals(package.Manifest.Id, StringComparison.OrdinalIgnoreCase))
                .Select(ToEnvironment));
        var merged = wrap.Merge(environments);
        if (string.IsNullOrWhiteSpace(merged.DefaultNixPath) && merged.Packages.Count == 0)
            return new CapabilityLaunchPlan(command, Arguments: arguments);

        var wrapped = wrap.Wrap(command, arguments, merged);
        return new CapabilityLaunchPlan(wrapped.FileName, Arguments: wrapped.Arguments);
    }

    private CapabilityLoadedModule? Load(string id)
    {
        var package = EnabledPackages()
            .FirstOrDefault(item => item.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return package is null ? null : loader.Load(package.PackageDirectory, package.Manifest);
    }

    public IReadOnlyList<string> RuntimePathPrefixes()
        => runtime.Directories(paths?.RegistryRoot, EnabledPackages().Select(package => package.PackageDirectory));

    public string? RuntimeRoot()
        => runtime.DevRoot(paths?.RegistryRoot);

    private static bool ProvidesCommand(CapabilityPackageManifest manifest, string fileName)
        => manifest.Provides.Any(name => name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    private LocalRegistryPackageDto ResolvePackage(string id, string? version)
    {
        if (!string.IsNullOrWhiteSpace(version))
            return local.Get(id, version)
                   ?? throw new InvalidOperationException($"Capability '{id}' version '{version}' is not in the local registry.");

        return local.ListPackages()
                   .Where(package => package.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(package => package.Manifest.Version, StringComparer.Ordinal)
                   .FirstOrDefault()
               ?? throw new InvalidOperationException($"Capability '{id}' is not in the local registry.");
    }

    private IReadOnlyList<LocalRegistryPackageDto> EnabledPackages()
    {
        var set = enabled.Read();
        return set.Modules
            .Select(module => local.Get(module.Id, module.Version))
            .Where(package => package is not null)
            .Cast<LocalRegistryPackageDto>()
            .ToArray();
    }

    private CapabilityModuleDto ToDto(LocalRegistryPackageDto package, EnabledCapabilitySetDto set)
    {
        var isEnabled = set.Modules.Any(module =>
            module.Id.Equals(package.Manifest.Id, StringComparison.OrdinalIgnoreCase)
            && module.Version.Equals(package.Manifest.Version, StringComparison.Ordinal));
        var windows = OperatingSystem.IsWindows();
        var messages = new List<string>();
        if (windows)
            messages.Add("Windows Server hosts capabilities through WSL2 or Linux; native Windows Nix is not supported.");
        else if (!nix.IsAvailable)
            messages.Add("Nix is not installed on this Server host.");

        var state = !isEnabled ? "disabled"
            : windows ? "error"
            : nix.IsAvailable ? "ready"
            : "error";
        return new CapabilityModuleDto(
            package.Manifest.Id,
            package.Manifest.Version,
            package.Manifest.DisplayName,
            package.Manifest.Publisher,
            package.Manifest.Kind,
            isEnabled,
            state,
            isEnabled && !windows && nix.IsAvailable,
            messages);
    }

    private static NixEnvironmentSpec ToEnvironment(LocalRegistryPackageDto package)
        => new(
            package.HasDefaultNix ? Path.Join(package.PackageDirectory, "default.nix") : null,
            package.Manifest.Nix?.Nixpkgs?.Rev,
            package.Manifest.Nix?.Packages ?? []);
}
