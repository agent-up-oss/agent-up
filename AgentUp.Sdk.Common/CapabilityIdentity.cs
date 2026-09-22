namespace AgentUp.Sdk.Common;

public sealed record CapabilityIdentity(
    string Id,
    string PackageVersion,
    string DisplayName,
    string Publisher);
