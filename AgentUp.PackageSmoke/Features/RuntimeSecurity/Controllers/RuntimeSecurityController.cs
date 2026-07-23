using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Shared.Interfaces;

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
        IFindingSink findings,
        CancellationToken cancellationToken = default)
        => await _checks.RunAsync(serverUrl, findings, cancellationToken);
}
