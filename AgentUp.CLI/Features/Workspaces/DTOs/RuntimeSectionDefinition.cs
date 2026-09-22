using System.Text.Json;

namespace AgentUp.CLI.Features.Workspaces.DTOs;

public sealed record RuntimeSectionDefinition(
    string ModuleId,
    IReadOnlyList<RuntimeSectionItem> Items);

public sealed record RuntimeSectionItem(
    string Name,
    string? TechnologyVersion = null,
    string? Path = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? EnvironmentFiles = null,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyList<string>? Volumes = null,
    IReadOnlyList<string>? ExtraArguments = null,
    bool Database = false,
    IReadOnlyDictionary<string, JsonElement>? Attributes = null);

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
