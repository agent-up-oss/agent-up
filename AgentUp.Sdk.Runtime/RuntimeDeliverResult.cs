namespace AgentUp.Sdk.Runtime;

public sealed record RuntimeDeliverResult(
    bool CanDeliver,
    string? NixPackage,
    IReadOnlyList<string> Messages);
