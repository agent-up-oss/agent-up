using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.NixRuntime.Controllers;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Forge;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Models;
using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Features.Capabilities.Services;

/// <summary>
/// Which built-in capabilities are enabled on this Server, whether each one can run, and the
/// launches they wrap. Every capability is always installed: enabling one is refused while the
/// software it runs cannot be found, and an enabled one whose software disappears stops offering
/// its feature rather than failing at launch.
/// </summary>
public sealed class CapabilityModuleService(
    ICapabilityCatalog catalog,
    ICapabilityEnabledSetStore enabled,
    ICapabilitySettingsStore settings,
    ICapabilitySoftwareProbe software,
    INixPresenceProvider nix,
    ICapabilityNixShellProvider shells,
    NixCapabilityEnvironmentController wrap,
    CapabilityRuntimePathProvider runtime) : IEnabledCapabilityPackages
{
    public IReadOnlyList<CapabilityModuleDto> List()
    {
        var set = EnabledIds();
        return catalog.Modules.Select(module => ToDto(module, set)).ToArray();
    }

    public CapabilityChangeResult Enable(string id)
    {
        var module = Find(id);
        if (module is null)
            return CapabilityChangeResult.NotFound(id);

        var missing = software.Missing(module.Software);
        if (missing.Count > 0)
        {
            return new CapabilityChangeResult(
                CapabilityChangeStatus.Unavailable,
                ToDto(module, EnabledIds()),
                CapabilityPrerequisiteMessages.SoftwareMissing(module.Identity.Id, missing));
        }

        var set = enabled.Read();
        enabled.Write(new EnabledCapabilitySetDto
        {
            Modules = set.Modules
                .Where(item => !item.Id.Equals(module.Identity.Id, StringComparison.OrdinalIgnoreCase))
                .Append(new CapabilityPackageRef(module.Identity.Id, module.Identity.PackageVersion))
                .ToArray()
        });
        return CapabilityChangeResult.Changed(ToDto(module, EnabledIds()));
    }

    public CapabilityChangeResult Disable(string id)
    {
        var module = Find(id);
        if (module is null)
            return CapabilityChangeResult.NotFound(id);

        var set = enabled.Read();
        enabled.Write(new EnabledCapabilitySetDto
        {
            Modules = set.Modules.Where(item => !item.Id.Equals(module.Identity.Id, StringComparison.OrdinalIgnoreCase)).ToArray()
        });
        return CapabilityChangeResult.Changed(ToDto(module, EnabledIds()));
    }

    public CapabilityChangeResult UpdateSettings(string id, IReadOnlyDictionary<string, string?> values)
    {
        var module = Find(id);
        if (module is null)
            return CapabilityChangeResult.NotFound(id);

        var errors = new List<string>();
        var stored = new Dictionary<string, string>(settings.Read(module.Identity.Id), StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            var spec = module.Settings.FirstOrDefault(item => item.Key.Equals(key, StringComparison.Ordinal));
            if (spec is null)
            {
                errors.Add($"Capability '{module.Identity.Id}' has no setting '{key}'.");
                continue;
            }

            var trimmed = value?.Trim() ?? "";
            if (trimmed.Length == 0)
            {
                stored.Remove(spec.Key);
                continue;
            }

            if (spec.Kind == CapabilitySettingKind.Url && !IsHttpUrl(trimmed))
            {
                errors.Add($"{spec.Label} must be an absolute http or https URL.");
                continue;
            }

            stored[spec.Key] = trimmed;
        }

        if (errors.Count > 0)
            return new CapabilityChangeResult(CapabilityChangeStatus.Invalid, ToDto(module, EnabledIds()), errors);

        settings.Write(module.Identity.Id, stored);
        return CapabilityChangeResult.Changed(ToDto(module, EnabledIds()));
    }

    public CapabilityReadinessDto? Readiness(string id)
    {
        var module = Find(id);
        if (module is null)
            return null;
        var values = settings.Read(module.Identity.Id);
        return new CapabilityReadinessDto(
            module.Identity.Id,
            module.Identity.DisplayName,
            EnabledIds().Contains(module.Identity.Id),
            software.Missing(module.Software),
            MissingSettingLabels(module, values));
    }

    public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
    {
        var providers = Enabled<IRuntimeCapability>()
            .Where(runtime => runtime.Provides.Any(name => name.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            .Select(runtime => (runtime.Identity.Id, runtime.NixPackages))
            .ToArray();
        if (providers.Length == 0 || !nix.IsAvailable)
            return new CapabilityLaunchWrapDto(fileName, arguments);

        var wrapped = wrap.Wrap(fileName, arguments, ToEnvironment(providers));
        return new CapabilityLaunchWrapDto(wrapped.FileName, wrapped.Arguments);
    }

    public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
    {
        var packages = NixPackagesOf(id);
        if (packages is null || !nix.IsAvailable)
            return new CapabilityLaunchWrapDto(fileName, arguments);

        var wrapped = wrap.Wrap(fileName, arguments, ToEnvironment([(id, packages)]));
        return new CapabilityLaunchWrapDto(wrapped.FileName, wrapped.Arguments);
    }

    public IRuntimeCapability? GetRuntime(string id) => FindEnabled<IRuntimeCapability>(id);

    public IReadOnlyList<IRuntimeCapability> ListRuntimes() => Enabled<IRuntimeCapability>();

    public IAgentCapability? GetAgent(string id) => FindEnabled<IAgentCapability>(id);

    public IReadOnlyList<IAgentCapability> ListAgents() => Enabled<IAgentCapability>();

    public IReadOnlyList<IAgentCapability> AllAgents() => catalog.Modules.OfType<IAgentCapability>().ToArray();

    public IForgeCapability? GetForge(string id) => FindEnabled<IForgeCapability>(id);

    public IReadOnlyList<IForgeCapability> ListForges() => Enabled<IForgeCapability>();

    /// <summary>
    /// The agent's ACP process, wrapped in its own Nix shell merged with every enabled runtime's,
    /// so an agent can run the toolchains the workspace's applications use.
    /// </summary>
    public CapabilityLaunchPlan? AgentLaunch(string id)
    {
        var agent = GetAgent(id);
        if (agent is null)
            return null;

        var launched = agent.Launch(settings.Read(agent.Identity.Id));
        var environment = launched.Environment;
        if (!nix.IsAvailable)
            return new CapabilityLaunchPlan(launched.FileName, Environment: environment, Arguments: launched.Arguments);

        var modules = Enabled<IRuntimeCapability>()
            .Select(runtime => (runtime.Identity.Id, runtime.NixPackages))
            .Prepend((agent.Identity.Id, agent.NixPackages))
            .ToArray();
        if (modules.All(module => module.NixPackages.Count == 0))
            return new CapabilityLaunchPlan(launched.FileName, Environment: environment, Arguments: launched.Arguments);

        var wrapped = wrap.Wrap(launched.FileName, launched.Arguments, ToEnvironment(modules));
        return new CapabilityLaunchPlan(wrapped.FileName, Environment: environment, Arguments: wrapped.Arguments);
    }

    public IReadOnlyList<string> RuntimePathPrefixes() => runtime.Directories();

    public string? RuntimeRoot() => runtime.DevRoot;

    private CapabilityModuleDto ToDto(ICapability module, IReadOnlySet<string> set)
    {
        var id = module.Identity.Id;
        var isEnabled = set.Contains(id);
        var missing = software.Missing(module.Software);
        var values = settings.Read(id);
        var missingSettings = MissingSettingLabels(module, values);
        var state = missing.Count > 0 ? CapabilityModuleStates.Unavailable
            : !isEnabled ? CapabilityModuleStates.Disabled
            : missingSettings.Count > 0 ? CapabilityModuleStates.NeedsSetup
            : CapabilityModuleStates.Ready;
        IReadOnlyList<string> messages = state switch
        {
            CapabilityModuleStates.Unavailable => CapabilityPrerequisiteMessages.SoftwareMissing(id, missing),
            CapabilityModuleStates.Disabled => CapabilityPrerequisiteMessages.NotEnabled(id),
            CapabilityModuleStates.NeedsSetup => CapabilityPrerequisiteMessages.SettingsMissing(module.Identity.DisplayName, missingSettings),
            _ => []
        };
        return new CapabilityModuleDto(
            id,
            module.Identity.PackageVersion,
            module.Identity.DisplayName,
            module.Identity.Publisher,
            module.Kind,
            isEnabled,
            missing.Count == 0,
            state,
            state == CapabilityModuleStates.Ready,
            missing,
            module.Settings.Select(spec => ToSetting(spec, values)).ToArray(),
            messages);
    }

    private static CapabilitySettingDto ToSetting(CapabilitySettingSpec spec, IReadOnlyDictionary<string, string> values)
    {
        var hasValue = values.TryGetValue(spec.Key, out var value) && !string.IsNullOrWhiteSpace(value);
        var shown = spec.Kind == CapabilitySettingKind.Secret || !hasValue ? null : value;
        return new CapabilitySettingDto(spec.Key, spec.Label, spec.Kind, spec.Required, spec.Placeholder, shown, hasValue);
    }

    private static IReadOnlyList<string> MissingSettingLabels(ICapability module, IReadOnlyDictionary<string, string> values)
    {
        var keys = CapabilitySettingSpec.MissingRequired(module.Settings, values);
        return module.Settings.Where(spec => keys.Contains(spec.Key)).Select(spec => spec.Label).ToArray();
    }

    private static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private ICapability? Find(string id)
        => catalog.Modules.FirstOrDefault(module => module.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private T? FindEnabled<T>(string id) where T : class, ICapability
        => Enabled<T>().FirstOrDefault(module => module.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<T> Enabled<T>() where T : ICapability
    {
        var set = EnabledIds();
        return catalog.Modules.OfType<T>().Where(module => set.Contains(module.Identity.Id)).ToArray();
    }

    private IReadOnlySet<string> EnabledIds()
        => enabled.Read().Modules.Select(module => module.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<NixPackageDeclaration>? NixPackagesOf(string id)
        => FindEnabled<ICapability>(id) switch
        {
            IRuntimeCapability runtimeModule => runtimeModule.NixPackages,
            IAgentCapability agent => agent.NixPackages,
            _ => null
        };

    /// <summary>
    /// One pinned shell holding every listed capability's packages. The shell is generated per
    /// combination, because a shell file can only name one package set.
    /// </summary>
    private NixEnvironmentSpec ToEnvironment(IReadOnlyList<(string Id, IReadOnlyList<NixPackageDeclaration> NixPackages)> modules)
    {
        var names = modules
            .SelectMany(module => module.NixPackages.Select(package => package.Package))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var key = string.Join("+", modules.Select(module => module.Id));
        return new NixEnvironmentSpec(shells.ShellPath(key, names), FirstPartyNixPin.Rev, names);
    }
}
