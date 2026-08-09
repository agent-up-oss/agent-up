using LocalInstaller.Smoke.Features.RuntimeSecurity.Interfaces;
using LocalInstaller.Smoke.Shared.Interfaces;

namespace LocalInstaller.Smoke.Tests.Features.RuntimeSecurity.Fake;

internal sealed class NullRuntimeSecurityChecks : IRuntimeSecurityChecks
{
    public Task RunAsync(string serverUrl, IFindingSink findings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
