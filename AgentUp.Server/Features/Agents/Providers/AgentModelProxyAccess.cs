using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// The unguessable key that authorises a sandboxed agent to use the Server's model proxy.
/// It lives only in memory and in the agent's environment, never on disk.
/// </summary>
public sealed class AgentModelProxyAccess
{
    public string Key { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public bool Matches(string? candidate)
    {
        if (candidate is null || candidate.Length != Key.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Key),
            Encoding.ASCII.GetBytes(candidate));
    }
}
