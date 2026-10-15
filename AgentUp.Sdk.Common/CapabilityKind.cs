namespace AgentUp.Sdk.Common;

public static class CapabilityKind
{
    public const string Runtime = "runtime";
    public const string Agent = "agent";
    public const string Forge = "forge";

    public static readonly IReadOnlyList<string> Allowed = [Runtime, Agent, Forge];
}
