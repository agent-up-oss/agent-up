using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>
/// Supplies a repository's static verification rules.
/// </summary>
/// <remarks>
/// An interface so resolution and guard behaviour can be covered without touching a
/// filesystem, which is what keeps those tests in the Unit tier.
/// </remarks>
public interface IVerificationConfigurationLoader
{
    VerificationConfiguration Load(string repositoryRoot);
}
