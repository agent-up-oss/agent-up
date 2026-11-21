using AgentUp.CLI.Features.Capabilities.DTOs;

namespace AgentUp.CLI.Tests.Support;

internal sealed class CapabilityModuleDtoBuilder
{
    private string _id = CliDomain.CapabilityId;
    private string _version = CliDomain.CapabilityVersion;
    private string _displayName = CliDomain.CapabilityDisplayName;
    private string _publisher = "agent-up";
    private string _kind = "runtime";
    private bool _enabled;
    private bool _available = true;
    private IReadOnlyList<string> _missing = [];
    private string _state = "disabled";
    private bool _canRun;
    private IReadOnlyList<string> _messages = ["Capability 'dotnet' is not enabled.", "Run: agent-up capabilities bootstrap"];

    public CapabilityModuleDtoBuilder Named(string id)
    {
        _id = id;
        return this;
    }

    public CapabilityModuleDtoBuilder Versioned(string version)
    {
        _version = version;
        return this;
    }

    public CapabilityModuleDtoBuilder Enabled()
    {
        _enabled = true;
        _state = "ready";
        _canRun = true;
        _messages = [];
        return this;
    }

    /// <summary>The Server did not find what the capability runs, so it cannot be enabled.</summary>
    public CapabilityModuleDtoBuilder Missing(params string[] missing)
    {
        _available = false;
        _missing = missing;
        _state = "unavailable";
        _canRun = false;
        _messages = [$"Capability '{_id}' needs Nix to provide its toolchain, and Nix was not found."];
        return this;
    }

    public CapabilityModuleDtoBuilder UnableToRun(params string[] messages)
    {
        _canRun = false;
        _state = _enabled ? "unavailable" : "disabled";
        _messages = messages;
        return this;
    }

    public CapabilityModuleDto Build()
        => new(_id, _version, _displayName, _publisher, _kind, _enabled, _available, _state, _canRun, _missing, _messages);
}
