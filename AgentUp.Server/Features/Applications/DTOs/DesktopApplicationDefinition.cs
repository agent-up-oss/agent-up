using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Features.Applications.DTOs;

public sealed record DesktopApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    DesktopWindowDefinition? Window = null,
    string Runtime = "linux",
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? EnvironmentFiles = null,
    string? Install = null);

public sealed record DesktopWindowDefinition(int Width = 1280, int Height = 800);
