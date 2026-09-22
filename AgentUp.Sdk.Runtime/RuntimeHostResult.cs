namespace AgentUp.Sdk.Runtime;

public sealed record RuntimeHostResult(
    bool CanRun,
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> Messages,
    string? NixPackage = null);
