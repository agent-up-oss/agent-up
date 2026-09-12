namespace AgentUp.AUDebug.Features.Test.DTOs;

public sealed record DebugTestStepDto(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);
