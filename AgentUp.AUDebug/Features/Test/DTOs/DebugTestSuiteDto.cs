namespace AgentUp.AUDebug.Features.Test.DTOs;

public sealed record DebugTestSuiteDto(
    string Id,
    string Title,
    IReadOnlyList<DebugTestStepDto> Steps);
