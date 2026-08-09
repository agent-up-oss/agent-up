using LocalInstaller.Smoke.Features.RuntimeSecurity.Interfaces;
using LocalInstaller.Smoke.Shared.Interfaces;

namespace LocalInstaller.Smoke.Features.RuntimeSecurity.Controllers;

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
