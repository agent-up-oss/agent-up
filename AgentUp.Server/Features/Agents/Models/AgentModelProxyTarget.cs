namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// Where the Server forwards a sandboxed agent's model HTTP. <see cref="Origin"/> is
/// scheme-host-port only; the rest of the request path is appended. When
/// <see cref="BearerToken"/> is set the Server holds the credential and the agent never
/// sees it. Otherwise the agent's own Authorization and vendor headers are forwarded.
/// </summary>
public sealed record AgentModelProxyTarget(string Origin, string? BearerToken);
