using AgentUp.Desktop.Features.Capabilities.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds a Server capability so tests say "enabled", "software not found" or "with a setting"
/// instead of restating the twelve-field production constructor.
/// </summary>
internal sealed class CapabilityModuleDtoBuilder
{
    private string _id = "dotnet";
    private string _displayName = ".NET";
    private string _kind = "runtime";
    private bool _enabled;
    private bool _available = true;
    private string _state = "disabled";
    private IReadOnlyList<string> _missing = [];
    private List<CapabilitySettingDto> _settings = [];
    private IReadOnlyList<string> _messages = ["not enabled"];

    public CapabilityModuleDtoBuilder Named(string id, string displayName, string kind = "runtime")
    {
        _id = id;
        _displayName = displayName;
        _kind = kind;
        return this;
    }

    public CapabilityModuleDtoBuilder Enabled()
    {
        _enabled = true;
        _state = _available ? "ready" : "unavailable";
        if (_available)
            _messages = [];
        return this;
    }

    /// <summary>The Server did not find what this capability runs.</summary>
    public CapabilityModuleDtoBuilder Missing(params string[] missing)
    {
        _available = false;
        _missing = missing;
        _state = "unavailable";
        _messages = ["Capability needs Nix to provide its toolchain, and Nix was not found.", "Install Nix: https://nixos.org/download"];
        return this;
    }

    public CapabilityModuleDtoBuilder NeedsSetup()
    {
        _state = "needsSetup";
        _messages = ["Set Endpoint for OpenCode in Settings."];
        return this;
    }

    public CapabilityModuleDtoBuilder WithSetting(string key, string kind, string? value = null, bool hasValue = false)
    {
        _settings.Add(new CapabilitySettingDto(key, char.ToUpperInvariant(key[0]) + key[1..], kind, kind != "secret", null, value, hasValue || value is not null));
        return this;
    }

    public CapabilityModuleDto Build()
        => new(_id, "1.0.0", _displayName, "agent-up", _kind, _enabled, _available, _state, _state == "ready", _missing, [.. _settings], _messages);
}
