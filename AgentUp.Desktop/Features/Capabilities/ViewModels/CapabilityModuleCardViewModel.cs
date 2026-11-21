using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Capabilities.ViewModels;

public sealed class CapabilityModuleCardViewModel : ReactiveObject
{
    public CapabilityModuleCardViewModel(CapabilityModuleDto module, CapabilityModulesViewModel catalog)
    {
        Id = module.Id;
        Version = module.Version;
        DisplayName = module.DisplayName;
        Publisher = module.Publisher;
        Enabled = module.Enabled;
        Available = module.Available;
        State = module.State;
        CanRun = module.CanRun;
        Missing = module.Missing;
        Summary = module.Messages.Count == 0 ? null : string.Join(" ", module.Messages);
        Settings = new ObservableCollection<CapabilitySettingFieldViewModel>(
            module.Settings.Select(setting => new CapabilitySettingFieldViewModel(module.Id, setting)));
        EnableCommand = ReactiveCommand.CreateFromTask(
            () => catalog.EnableAsync(Id),
            this.WhenAnyValue(card => card.Available));
        DisableCommand = ReactiveCommand.CreateFromTask(() => catalog.DisableAsync(Id));
        SaveSettingsCommand = ReactiveCommand.CreateFromTask(() => catalog.SaveSettingsAsync(Id, ChangedSettings()));
    }

    public string Id { get; }

    public string EnableAutomationId => DesktopAutomationIds.CapabilityEnable(Id);

    public string DisableAutomationId => DesktopAutomationIds.CapabilityDisable(Id);

    public string SaveAutomationId => DesktopAutomationIds.CapabilitySave(Id);

    public string Version { get; }

    public string DisplayName { get; }

    public string Publisher { get; }

    public bool Enabled { get; }

    /// <summary>Whether the Server found the software this capability runs. Enabling is refused until it does.</summary>
    public bool Available { get; }

    public string State { get; }

    public bool CanRun { get; }

    public IReadOnlyList<string> Missing { get; }

    /// <summary>The Server's explanation, shown as a tooltip rather than as prose on the card.</summary>
    public string? Summary { get; }

    /// <summary>
    /// The enable button's own label. A capability whose software was not found says so in the
    /// button instead of offering an action that would be refused.
    /// </summary>
    public string EnableLabel => Available ? "Enable" : Missing.Contains("nix") ? "Needs Nix" : "Not found";

    /// <summary>Enable is offered only for a disabled capability whose software was found.</summary>
    public bool CanEnable => !Enabled && Available;

    /// <summary>A disabled capability whose software is missing shows a neutral, inert button instead.</summary>
    public bool IsUnavailable => !Enabled && !Available;

    /// <summary>One short status; a capability whose software is missing names it rather than explaining.</summary>
    public string StatusLabel => State switch
    {
        "ready" => "Ready",
        "needsSetup" => "Needs setup",
        "unavailable" => Missing.Count == 0 ? "Not found" : "Missing " + string.Join(", ", Missing),
        _ => "Off"
    };

    public ObservableCollection<CapabilitySettingFieldViewModel> Settings { get; }

    /// <summary>Settings expand once the capability is enabled, because they only matter then.</summary>
    public bool ShowsSettings => Enabled && Settings.Count > 0;

    public ReactiveCommand<Unit, Unit> EnableCommand { get; }

    public ReactiveCommand<Unit, Unit> DisableCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }

    public IReadOnlyDictionary<string, string> ChangedSettings()
        => Settings.Where(field => field.IsChanged).ToDictionary(field => field.Key, field => field.Value.Trim());
}
