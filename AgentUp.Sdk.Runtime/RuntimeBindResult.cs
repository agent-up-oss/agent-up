namespace AgentUp.Sdk.Runtime;

public sealed record RuntimeBindResult(
    bool IsValid,
    IReadOnlyList<string> Messages,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Items);
