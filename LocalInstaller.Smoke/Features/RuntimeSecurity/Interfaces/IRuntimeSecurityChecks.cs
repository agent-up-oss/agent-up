using AgentUp.PackageSmoke.Shared.Interfaces;

namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

public interface IRuntimeSecurityChecks
{
    Task RunAsync(string serverUrl, IFindingSink findings, CancellationToken cancellationToken = default);
}
