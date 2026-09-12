using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Produces the content hashes recorded in receipts and recomputed by the guard.
/// </summary>
/// <remarks>
/// Both forms must agree, because a receipt written from working-tree bytes is later
/// compared against a hash that may have been produced from a stored patch instead.
/// </remarks>
public sealed class ContentHashProvider
{
    private const string Prefix = "sha256:";

    public string HashBytes(ReadOnlySpan<byte> content)
        => Prefix + Convert.ToHexStringLower(SHA256.HashData(content));

    public string HashText(string content)
        => HashBytes(Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// Hashes a file's current bytes, or returns the deletion marker when it is gone.
    /// A deleted file must still take part in the covered map: removing a file is a change
    /// the checks need to be re-proven against.
    /// </summary>
    public string HashFile(string absolutePath)
        => File.Exists(absolutePath)
            ? HashBytes(File.ReadAllBytes(absolutePath))
            : DeletedMarker;

    public static string DeletedMarker => "absent";
}
