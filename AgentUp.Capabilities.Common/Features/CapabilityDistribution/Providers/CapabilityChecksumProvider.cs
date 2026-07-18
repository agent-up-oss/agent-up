using System.Security.Cryptography;

namespace AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;

public sealed class CapabilityChecksumProvider
{
    public async Task<bool> MatchesSha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(actual, expectedSha256.ToLowerInvariant(), StringComparison.Ordinal);
    }
}
