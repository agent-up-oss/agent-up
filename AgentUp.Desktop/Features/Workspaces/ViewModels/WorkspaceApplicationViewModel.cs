using AgentUp.Desktop.Features.Ports.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceApplicationViewModel : ReactiveObject
{
    private string _command;
    private string _state;
    private string _stateColor;
    private IReadOnlyList<PortMappingDto> _allocatedPorts;

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

    public WorkspaceApplicationViewModel(
        string name,
        string command,
        string state,
        IReadOnlyList<PortMappingDto>? allocatedPorts = null)
    {
        Name = name;
        _command = command;
        _state = state;
        _stateColor = ResolveStateColor(state);
        _allocatedPorts = allocatedPorts ?? [];
    }

    public bool UpdateFrom(string command, string state, IReadOnlyList<PortMappingDto>? allocatedPorts)
    {
        var ports = allocatedPorts ?? [];
        var portsChanged = !AllocatedPorts.SequenceEqual(ports);

        Command = command;
        AllocatedPorts = ports;
        UpdateState(state);

        return portsChanged;
    }

    public void UpdateState(string newState)
    {
        State = newState;
        StateColor = ResolveStateColor(newState);
    }

    private static string ResolveStateColor(string state) => state switch
    {
        "Running" => "#00d66b",
        "Failed" => "#b85a5a",
        _ => "#5a5a72"
    };
}
