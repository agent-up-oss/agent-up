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
    private bool _database;
    private CapabilityStatusDto? _capabilityStatus;

    public string Name { get; }
    public bool IsDesktop { get; private set; }

    public bool Database
    {
        get => _database;
        private set => this.RaiseAndSetIfChanged(ref _database, value);
    }

    public CapabilityStatusDto? CapabilityStatus
    {
        get => _capabilityStatus;
        private set => this.RaiseAndSetIfChanged(ref _capabilityStatus, value);
    }

    public string? CapabilitySummary
        => CapabilityStatus is { CanRun: false, Messages.Count: > 0 }
            ? string.Join(" ", CapabilityStatus.Messages)
            : null;

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
        bool database = false,
        IReadOnlyList<PortMappingDto>? allocatedPorts = null,
        IReadOnlyList<PortHealthChangeDto>? portHealth = null,
        bool isDesktop = false,
        CapabilityStatusDto? capabilityStatus = null)
    {
        Name = name;
        IsDesktop = isDesktop;
        _database = database;
        _command = command;
        _state = state;
        _stateColor = AppHealthLedRules.StateColor(state);
        _allocatedPorts = allocatedPorts ?? [];
        _portHealth = portHealth;
        _capabilityStatus = capabilityStatus;
    }

    public bool UpdateFrom(
        string command,
        string state,
        IReadOnlyList<PortMappingDto>? allocatedPorts,
        bool database = false,
        bool isDesktop = false,
        CapabilityStatusDto? capabilityStatus = null)
    {
        var ports = allocatedPorts ?? [];
        var portsChanged = !AllocatedPorts.SequenceEqual(ports);
        var databaseChanged = Database != database;
        var desktopChanged = IsDesktop != isDesktop;
        var capabilityChanged = !Equals(CapabilityStatus, capabilityStatus);

        Command = command;
        AllocatedPorts = ports;
        Database = database;
        IsDesktop = isDesktop;
        CapabilityStatus = capabilityStatus;
        this.RaisePropertyChanged(nameof(CapabilitySummary));
        var stateChanged = UpdateState(state);
        return portsChanged || stateChanged || databaseChanged || desktopChanged || capabilityChanged;
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
