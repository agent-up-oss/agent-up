namespace AgentUp.Sdk.Agent;

public sealed record AgentLoginSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string Transport);
