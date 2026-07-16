using AgentUp.PackageSmoke.Features.Validation.Services;

namespace AgentUp.PackageSmoke.Features.Security.Services;

public interface IRuntimeSecurityChecks
{
    Task RunAsync(string serverUrl, FileAssertions assert, CancellationToken cancellationToken = default);
}
