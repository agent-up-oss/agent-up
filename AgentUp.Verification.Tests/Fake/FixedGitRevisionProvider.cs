using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Tests.Fake;

internal sealed class FixedGitRevisionProvider(string revision) : IGitRevisionProvider
{
    public string Resolve(string repositoryRoot) => revision;
}
