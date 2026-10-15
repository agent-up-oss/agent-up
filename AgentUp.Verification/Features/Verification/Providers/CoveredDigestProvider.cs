using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Digests the covered path-to-hash map into a single fingerprint. The export carries this
/// instead of the map so a reader can compare two reports without reconstructing hashes the
/// guard would accept.
/// </summary>
public sealed class CoveredDigestProvider
{
    public string Digest(IReadOnlyDictionary<string, string> covered)
    {
        var payload = string.Concat(
            covered.Keys.Order(StringComparer.Ordinal).Select(path => path + "\0" + covered[path] + "\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
