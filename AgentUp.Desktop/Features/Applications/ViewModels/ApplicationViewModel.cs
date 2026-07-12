namespace AgentUp.Desktop.Features.Applications.ViewModels;

public sealed class ApplicationViewModel
{
    public string Name { get; }
    public string Command { get; }
    public string? PortVariable { get; }
    public string State { get; }
    public string StateColor { get; }

    public ApplicationViewModel(string name, string command, string? portVariable, string state)
    {
        Name = name;
        Command = command;
        PortVariable = portVariable;
        State = state;
        StateColor = ResolveStateColor(state);
    }

    private static string ResolveStateColor(string state) => state switch
    {
        "Running" => "#4cbe78",
        "Starting" or "Stopping" => "#c8963c",
        "Failed" => "#b85a5a",
        _ => "#3a3a50"
    };
}
