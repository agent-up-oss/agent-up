using LocalInstaller.Smoke.Shared.Interfaces;

namespace LocalInstaller.Smoke.Features.RuntimeSecurity.Interfaces;

public interface IRuntimeSecurityChecks
{
    Task RunAsync(string serverUrl, IFindingSink findings, CancellationToken cancellationToken = default);
}
