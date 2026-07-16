using AgentUp.PackageSmoke.Features.Security;
using AgentUp.PackageSmoke.Features.Security.Services;
using AgentUp.PackageSmoke.Features.Validation;
using AgentUp.PackageSmoke.Features.Validation.Services;

namespace AgentUp.PackageSmoke.Tests.Features.Security;

internal sealed class NullRuntimeSecurityChecks : IRuntimeSecurityChecks
{
    public Task RunAsync(string serverUrl, FileAssertions assert, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
