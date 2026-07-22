using AgentUp.PackageSmoke.Features.PackageValidation.Services;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Controllers;

public sealed class RuntimeSecurityController
{
    private readonly IRuntimeSecurityChecks _checks;

    public RuntimeSecurityController(IRuntimeSecurityChecks checks)
    {
        _checks = checks;
    }

    public async Task RunAsync(
        string serverUrl,
        FileAssertions assert,
        CancellationToken cancellationToken = default)
        => await _checks.RunAsync(serverUrl, assert, cancellationToken);
}
