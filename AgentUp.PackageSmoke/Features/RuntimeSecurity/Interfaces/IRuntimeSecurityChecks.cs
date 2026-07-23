namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

public interface IRuntimeSecurityChecks
{
    Task RunAsync(string serverUrl, IRuntimeSecurityFindingSink findings, CancellationToken cancellationToken = default);
}
