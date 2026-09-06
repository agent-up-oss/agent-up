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
    public bool Database { get; private set; }

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
        IReadOnlyList<PortHealthChangeDto>? portHealth = null,
        bool database = false)
    {
        Name = name;
        _command = command;
        _state = state;
        _stateColor = AppHealthLedRules.StateColor(state);
        _allocatedPorts = allocatedPorts ?? [];
        _portHealth = portHealth;
        Database = database;
    }

    public bool UpdateFrom(string command, string state, IReadOnlyList<PortMappingDto>? allocatedPorts, bool database = false)
    {
        var ports = allocatedPorts ?? [];
        var portsChanged = !AllocatedPorts.SequenceEqual(ports);

        Command = command;
        AllocatedPorts = ports;
        var stateChanged = UpdateState(state);
        var databaseChanged = Database != database;
        Database = database;
        return portsChanged || stateChanged || databaseChanged;
    }

    internal bool UpdateState(string newState, IReadOnlyList<PortHealthChangeDto>? portHealth = null)
    {
        var stateUnchanged = string.Equals(State, newState, StringComparison.Ordinal);
        if (stateUnchanged && PortHealthEquivalent(PortHealth, portHealth))
            return false;

        PortHealth = portHealth;
        State = newState;
        StateColor = AppHealthLedRules.StateColor(newState);
        return true;
    }

    private static bool PortHealthEquivalent(
        IReadOnlyList<PortHealthChangeDto>? left,
        IReadOnlyList<PortHealthChangeDto>? right)
    {
        static IEnumerable<PortHealthChangeDto> Ordered(IReadOnlyList<PortHealthChangeDto>? items)
            => (items ?? []).OrderBy(p => p.AllocatedPort);

        return Ordered(left).SequenceEqual(Ordered(right));
    }
}
