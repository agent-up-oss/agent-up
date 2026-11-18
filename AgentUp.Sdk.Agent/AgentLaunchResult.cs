namespace AgentUp.Sdk.Agent;

/// <param name="Environment">
/// Variables the ACP process needs on top of the Server's own. A secret belongs here rather than
/// in <paramref name="Arguments"/>, because a command line is readable by every user on the host.
/// </param>
public sealed record AgentLaunchResult(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? NixPackage = null,
    IReadOnlyDictionary<string, string>? Environment = null);
