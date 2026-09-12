using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Fake;

internal sealed class StubConfigurationLoader(VerificationConfiguration configuration)
    : IVerificationConfigurationLoader
{
    public VerificationConfiguration Load(string repositoryRoot) => configuration;
}
