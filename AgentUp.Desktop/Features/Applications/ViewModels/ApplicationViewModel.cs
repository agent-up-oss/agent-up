using AgentUp.Desktop.Features.Ports.DTOs;

namespace AgentUp.Desktop.Features.Applications.ViewModels;

public sealed class ApplicationViewModel
{
    public string Name { get; }
    public string Command { get; }
    public string State { get; }
    public string StateColor { get; }
    public IReadOnlyList<PortMappingDto> AllocatedPorts { get; }

    public ApplicationViewModel(string name, string command, string state, IReadOnlyList<PortMappingDto>? allocatedPorts = null)
    {
        Name = name;
        Command = command;
        State = state;
        StateColor = ResolveStateColor(state);
        AllocatedPorts = allocatedPorts ?? [];
    }

    private static string ResolveStateColor(string state) => state switch
    {
        "Running" => "#00d66b",
        "Failed" => "#b85a5a",
        _ => "#5a5a72"
    };
}
