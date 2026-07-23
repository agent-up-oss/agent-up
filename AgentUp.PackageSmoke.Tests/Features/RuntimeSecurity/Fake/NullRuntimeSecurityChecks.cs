using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

namespace AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity.Fake;

internal sealed class NullRuntimeSecurityChecks : IRuntimeSecurityChecks
{
    public Task RunAsync(string serverUrl, IRuntimeSecurityFindingSink findings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
