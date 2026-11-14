namespace AgentUp.Desktop.Features.Interface.Models;

/// <summary>Where the Server says to send the reader to authorize.</summary>
public sealed record TaskSourceLogin(string AuthorizeUrl);

/// <summary>A repository host this Server could read a backlog from.</summary>
public sealed record TaskSourceModule(string ModuleId, string DisplayName);

/// <summary>Where this Server's agents take their work from, and whether it can be read.</summary>
public sealed record TaskSource(
    string? ModuleId,
    string? Owner,
    string? Repository,
    bool Configured,
    bool Connected,
    bool CanConnect,
    string Detail,
    IReadOnlyList<TaskSourceModule> Modules)
{
    public string Label => Owner is { Length: > 0 } && Repository is { Length: > 0 }
        ? $"{Owner}/{Repository}"
        : "Not set";
}
