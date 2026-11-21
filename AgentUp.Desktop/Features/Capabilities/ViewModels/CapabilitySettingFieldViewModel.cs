using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Capabilities.ViewModels;

/// <summary>
/// One capability setting as an editable field. A secret starts empty because the Server never
/// returns it; its placeholder says whether one is stored, and leaving it empty keeps that one.
/// </summary>
public sealed class CapabilitySettingFieldViewModel : ReactiveObject
{
    private readonly string _initial;
    private string _value;

    public CapabilitySettingFieldViewModel(string moduleId, CapabilitySettingDto setting)
    {
        Key = setting.Key;
        Label = setting.Required ? setting.Label : setting.Label + " (optional)";
        IsSecret = setting.Kind == "secret";
        _initial = IsSecret ? "" : setting.Value ?? "";
        _value = _initial;
        Placeholder = IsSecret && setting.HasValue ? "Stored" : setting.Placeholder ?? "";
        AutomationId = DesktopAutomationIds.CapabilitySetting(moduleId, setting.Key);
    }

    public string Key { get; }

    public string Label { get; }

    public bool IsSecret { get; }

    public string Placeholder { get; }

    public string AutomationId { get; }

    public char PasswordChar => IsSecret ? '•' : '\0';

    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    /// <summary>Whether saving should send this field. An untouched secret is left out so it is kept.</summary>
    public bool IsChanged => IsSecret ? Value.Length > 0 : !Value.Equals(_initial, StringComparison.Ordinal);
}
