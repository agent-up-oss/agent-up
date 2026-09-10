using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Server.Features.Audit.Providers;

internal sealed class AuditApplicationIndexPathsProvider(string auditRoot)
{
    private readonly string _auditRoot = Path.GetFullPath(auditRoot);

    internal string GetDailyIndexFile(string workspaceId, string application, DateOnly date)
    {
        var storeRoot = GetApplicationIndexDirectory(workspaceId, application);
        return Path.Join(storeRoot, $"{date:yyyy-MM-dd}.idx.jsonl");
    }

    internal string GetApplicationIndexDirectory(string workspaceId, string application)
    {
        ValidateSegment(workspaceId, nameof(workspaceId));
        var applicationKey = EncodeApplicationKey(application);
        var path = Path.GetFullPath(Path.Join(_auditRoot, "indexes", workspaceId, applicationKey));
        EnsureUnderAuditRoot(path);
        return path;
    }

    internal static string EncodeApplicationKey(string application)
    {
        if (string.IsNullOrWhiteSpace(application))
            throw new InvalidOperationException("Application name must not be empty.");

        var normalized = application.Trim().ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return hash[..16].ToLowerInvariant();
    }

    private void EnsureUnderAuditRoot(string path)
    {
        var relative = Path.GetRelativePath(_auditRoot, path);
        if (relative == ".."
            || relative.StartsWith("../", StringComparison.Ordinal)
            || relative.StartsWith("..\\", StringComparison.Ordinal))
            throw new InvalidOperationException("Audit index paths must stay under the audit root.");
    }

    private static void ValidateSegment(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} must not be empty.");

        if (value.Contains("..", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal))
            throw new InvalidOperationException($"{name} contains unsafe path characters.");
    }
}
