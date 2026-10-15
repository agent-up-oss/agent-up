namespace AgentUp.Sdk.Forge;

public sealed record ForgeRequest(string Token, string Owner, string Repository, string Branch);
