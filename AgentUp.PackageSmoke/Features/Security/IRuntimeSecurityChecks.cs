using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.Security;

public interface IRuntimeSecurityChecks
{
    Task RunAsync(string serverUrl, FileAssertions assert, CancellationToken cancellationToken = default);
}
