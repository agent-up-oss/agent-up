namespace AgentUp.Server.Features.Forge.Models;

public sealed record GithubAppConnection(
    string WorkspaceId,
    string State,
    string SetupKey,
    string AppName,
    string CallbackOrigin,
    string ApiBaseUrl,
    string WebOrigin,
    long? AppId = null,
    string? Slug = null,
    string? PrivateKey = null,
    long? InstallationId = null,
    string? RegistrationOwner = null,
    string? RepositoryOwner = null);
