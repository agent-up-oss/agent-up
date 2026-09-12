using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Features.Coverage.Interfaces;

/// <summary>
/// Supplies the repository's coverage rules. An interface so threshold behaviour is
/// testable without a filesystem, keeping those tests in the Unit tier.
/// </summary>
public interface ICoverageConfigurationLoader
{
    CoverageConfiguration Load(string repositoryRoot);
}
