using AgentUp.AUDebug.Features.Test.DTOs;

namespace AgentUp.AUDebug.Features.Test.Interfaces;

public interface IDebugTestSuiteCatalog
{
    IReadOnlyList<string> SuiteIds { get; }
    IReadOnlyList<string> BuildIds { get; }
    bool TryResolve(string suite, out IReadOnlyList<DebugTestSuiteDto> suites, out string? error);
    bool TryResolveBuild(string target, out IReadOnlyList<DebugTestSuiteDto> suites, out string? error);
}
