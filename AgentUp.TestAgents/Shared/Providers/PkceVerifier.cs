using System.Security.Cryptography;
using System.Text;

using AgentUp.TestAgents.Shared.Providers;

namespace AgentUp.TestAgents.Shared.Providers;

/// <summary>
/// PKCE S256 as RFC 7636 defines it. Real, not stubbed: a test agent that computed its challenge
/// wrongly has to fail here the same way it would against the real provider.
/// </summary>
public static class PkceVerifier
{
    public static string Challenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static bool Matches(string? challenge, string? verifier) =>
        !string.IsNullOrEmpty(challenge)
        && !string.IsNullOrEmpty(verifier)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Challenge(verifier)),
            Encoding.ASCII.GetBytes(challenge));

    public static string Secret(int bytes = 32) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>A short code a person can read off a screen and type, with no ambiguous glyphs.</summary>
    public static string UserCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = RandomNumberGenerator.GetItems<char>(alphabet, 8);
        return $"{new string(code[..4])}-{new string(code[4..])}";
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
