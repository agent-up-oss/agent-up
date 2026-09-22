namespace AgentUp.Sdk.Runtime;

public sealed record RuntimePortMapping(string? Variable, int DefaultPort, int AllocatedPort);
