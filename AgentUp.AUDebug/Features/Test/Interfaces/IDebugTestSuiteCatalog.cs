using AgentUp.AUDebug.Features.Test.DTOs;

namespace AgentUp.AUDebug.Features.Test.Interfaces;

public interface IDebugTestSuiteCatalog
{
    IReadOnlyList<string> SuiteIds { get; }
    bool TryResolve(string suite, out IReadOnlyList<DebugTestSuiteDto> suites, out string? error);
}
