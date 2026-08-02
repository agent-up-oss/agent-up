using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Server.Features.Audit.Providers;

public sealed class AuditWorkdirIdProvider
{
    public string Create(string worktreePath)
    {
        var normalized = Path.GetFullPath(worktreePath);
        var comparisonPath = OperatingSystem.IsWindows()
            ? normalized.ToUpperInvariant()
            : normalized;
        var bytes = Encoding.UTF8.GetBytes(comparisonPath);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..16];
    }
}
