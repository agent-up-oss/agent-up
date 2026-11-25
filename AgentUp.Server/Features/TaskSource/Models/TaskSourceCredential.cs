namespace AgentUp.Server.Features.TaskSource.Models;

/// <summary>
/// The personal access token this Server holds for one host, and the origin it talks to.
/// </summary>
public sealed record TaskSourceCredential(string Token, string EndpointUrl);
