using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Applications.ViewModels;

public sealed class ApplicationViewModel : ReactiveObject
{
    private string _state;
    private string _stateColor;

    public string Name { get; }
    public string Command { get; }
    public bool Database { get; }
    public bool IsDesktop { get; }
    public IReadOnlyList<PortMappingDto> AllocatedPorts { get; }

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

    public ApplicationViewModel(
        string name,
        string command,
        string state,
        IReadOnlyList<PortMappingDto>? allocatedPorts = null,
        bool database = false,
        bool isDesktop = false)
    {
        Name = name;
        Command = command;
        Database = database;
        IsDesktop = isDesktop;
        _state = state;
        _stateColor = AppHealthLedRules.StateColor(state);
        AllocatedPorts = allocatedPorts ?? [];
    }

    public void UpdateState(string newState)
    {
        State = newState;
        StateColor = AppHealthLedRules.StateColor(newState);
    }
}
