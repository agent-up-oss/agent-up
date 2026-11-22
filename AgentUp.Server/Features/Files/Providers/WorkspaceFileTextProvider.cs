using System.Security.Cryptography;
using System.Text;
using AgentUp.Server.Features.Files.Models;

namespace AgentUp.Server.Features.Files.Providers;

/// <summary>
/// Decides whether a file's bytes are text a person can read and edit, and turns them into that
/// text and back without changing anything the edit did not touch: a UTF-8 byte order mark stays,
/// and line endings are whatever the text carries.
/// </summary>
public sealed class WorkspaceFileTextProvider
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The text of a UTF-8 file, or null when the bytes are binary or not UTF-8.</summary>
    public WorkspaceFileText? Decode(byte[] bytes)
    {
        var probe = Math.Min(bytes.Length, WorkspaceFileLimits.BinaryProbeBytes);
        if (Array.IndexOf(bytes, (byte)0, 0, probe) >= 0)
            return null;
        var hasBom = bytes.AsSpan().StartsWith(Bom);
        try
        {
            var text = Strict.GetString(bytes, hasBom ? Bom.Length : 0, bytes.Length - (hasBom ? Bom.Length : 0));
            return new WorkspaceFileText(text, hasBom);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    public byte[] Encode(string text, bool hasBom)
    {
        var body = Strict.GetBytes(text);
        return hasBom ? [.. Bom, .. body] : body;
    }

    /// <summary>Names these exact bytes, so a save can prove it was edited from what is on disk.</summary>
    public string Version(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The version of a file over the content limit, which names its size rather than bytes nobody read.</summary>
    public string SizeVersion(long size) => $"size-{size}";
}
