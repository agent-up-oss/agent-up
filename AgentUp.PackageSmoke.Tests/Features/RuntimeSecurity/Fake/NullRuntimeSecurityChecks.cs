using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Shared.Interfaces;

namespace AgentUp.PackageSmoke.Tests.Features.RuntimeSecurity.Fake;

internal sealed class NullRuntimeSecurityChecks : IRuntimeSecurityChecks
{
    public Task RunAsync(string serverUrl, IFindingSink findings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
