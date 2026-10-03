namespace AgentUp.Desktop.Features.Authentication.Models;

public sealed record ConnectionSource(
    string Id,
    string Kind,
    string BaseUrl,
    string DisplayName,
    string AuthMode,
    string ApiVersion,
    string WorkspacePresentation,
    string Prompt,
    bool IdentifierRequired,
    bool IsLegacy);
