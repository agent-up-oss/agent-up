namespace AgentUp.Sdk.Agent;

public sealed record AgentLaunchResult(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? NixPackage = null);
