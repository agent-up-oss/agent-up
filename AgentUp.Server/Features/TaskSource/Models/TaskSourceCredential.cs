namespace AgentUp.Server.Features.TaskSource.Models;

/// <summary>
/// The personal access token this Server holds for one identity, the origin it talks to, and
/// which host that token is for. <paramref name="ModuleId"/> is <c>github</c> or <c>forgejo</c>
/// when it is known; a legacy file keyed only by host leaves it empty and the store infers it
/// from the key.
/// </summary>
public sealed record TaskSourceCredential(string Token, string EndpointUrl, string ModuleId = "");
