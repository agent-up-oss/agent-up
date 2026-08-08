using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceApplicationViewModel : ReactiveObject
{
    private string _command;
    private string _state;
    private string _stateColor;
    private IReadOnlyList<PortMappingDto> _allocatedPorts;
    private IReadOnlyList<PortHealthChangeDto>? _portHealth;

    public string Name { get; }

    public string Command
    {
        get => _command;
        private set => this.RaiseAndSetIfChanged(ref _command, value);
    }

    public IReadOnlyList<PortMappingDto> AllocatedPorts
    {
        get => _allocatedPorts;
        private set => this.RaiseAndSetIfChanged(ref _allocatedPorts, value);
    }

    public string State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public string StateColor
    {
        get => _stateColor;
        private set => this.RaiseAndSetIfChanged(ref _stateColor, value);
    }

    internal IReadOnlyList<PortHealthChangeDto>? PortHealth
    {
        get => _portHealth;
        private set => this.RaiseAndSetIfChanged(ref _portHealth, value);
    }

    internal WorkspaceApplicationViewModel(
        string name,
        string command,
        string state,
        IReadOnlyList<PortMappingDto>? allocatedPorts = null,
        IReadOnlyList<PortHealthChangeDto>? portHealth = null)
    {
        Name = name;
        _command = command;
        _state = state;
        _stateColor = AppHealthLedRules.StateColor(state);
        _allocatedPorts = allocatedPorts ?? [];
        _portHealth = portHealth;
    }

    public bool UpdateFrom(string command, string state, IReadOnlyList<PortMappingDto>? allocatedPorts)
    {
        var ports = allocatedPorts ?? [];
        var portsChanged = !AllocatedPorts.SequenceEqual(ports);

        Command = command;
        AllocatedPorts = ports;
        var stateChanged = UpdateState(state);
        return portsChanged || stateChanged;
    }

    internal bool UpdateState(string newState, IReadOnlyList<PortHealthChangeDto>? portHealth = null)
    {
        PortHealth = portHealth;
        if (string.Equals(State, newState, StringComparison.Ordinal) && portHealth is null)
            return false;

        State = newState;
        StateColor = AppHealthLedRules.StateColor(newState);
        return true;
    }
}
