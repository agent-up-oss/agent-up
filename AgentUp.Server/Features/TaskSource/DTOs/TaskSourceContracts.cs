namespace AgentUp.Server.Features.TaskSource.DTOs;

/// <summary>
/// The task source as a client shows it: what it points at, whether it can be read, and where it
/// is stuck when it cannot.
/// </summary>
public sealed record TaskSourceDto(
    string? ModuleId,
    string? Owner,
    string? Repository,
    bool Configured,
    bool Connected,
    /// <summary>True when a sign-in can be started: a module is installed and OAuth is set up.</summary>
    bool CanConnect,
    string Detail,
    IReadOnlyList<TaskSourceModuleDto> Modules);

/// <summary>A repository host this Server could read a backlog from.</summary>
public sealed record TaskSourceModuleDto(string ModuleId, string DisplayName);

public sealed record SetTaskSourceRequest(string ModuleId, string Owner, string Repository);

/// <summary>Where to send the reader to authorize, or why nobody can be sent anywhere.</summary>
public sealed record TaskSourceLoginDto(string AuthorizeUrl);
