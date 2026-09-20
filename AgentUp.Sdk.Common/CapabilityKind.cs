namespace AgentUp.Sdk.Common;

public static class CapabilityKind
{
    public const string Runtime = "runtime";
    public const string Agent = "agent";

    public static readonly IReadOnlyList<string> Allowed = [Runtime, Agent];
}
