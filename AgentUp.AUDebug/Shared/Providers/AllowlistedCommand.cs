namespace AgentUp.AUDebug.Shared.Providers;

public sealed record AllowlistedCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string>? Environment = null,
    string? StandardInput = null);
