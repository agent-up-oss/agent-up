namespace AgentUp.Sdk.Forge;

/// <param name="EndpointUrl">
/// The host origin this request should talk to, such as https://github.com or a Forgejo origin.
/// Null keeps the capability's configured Host.
/// </param>
public sealed record ForgeRequest(
    string Token,
    string Owner,
    string Repository,
    string Branch,
    string? EndpointUrl = null);
