using AgentUp.Desktop.Features.Agents.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds an <see cref="AgentDescriptorDto"/> so tests state only availability,
/// not the six-parameter wire shape.
/// </summary>
internal sealed class AgentDescriptorDtoBuilder
{
    private string _agent = "Codex";
    private bool _available = true;
    private string _displayName = "Codex";
    private string? _unavailability;
    private string? _detail;
    private string? _remedy;

    public AgentDescriptorDtoBuilder Named(string agent, string? displayName = null)
    {
        _agent = agent;
        _displayName = displayName ?? agent;
        return this;
    }

    public AgentDescriptorDtoBuilder Available()
    {
        _available = true;
        _unavailability = null;
        _detail = null;
        _remedy = null;
        return this;
    }

    public AgentDescriptorDtoBuilder PackageDisabled()
    {
        _available = false;
        _unavailability = AgentDescriptorDto.CapabilityNotEnabled;
        _detail = $"{_displayName} is not enabled on this Server.";
        _remedy = $"Enable the {_agent} capability.";
        return this;
    }

    public AgentDescriptorDtoBuilder ExecutableMissing(string? command = null)
    {
        _available = false;
        _unavailability = AgentDescriptorDto.ExecutableNotFound;
        _detail = $"{_agent} ACP executable is not installed or is not on PATH.";
        _remedy = $"Install {command ?? _agent} and keep it on PATH.";
        return this;
    }

    public AgentDescriptorDto Build() =>
        new(_agent, _available, _displayName, _unavailability, _detail, _remedy);
}
