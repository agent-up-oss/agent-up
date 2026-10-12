using System.Security.Cryptography;
using AgentUp.TestAgents.Features.Inference.Interfaces;
using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// Keeps pinned GGUF weights under a cache root. A miss downloads from the documented mirror;
/// a SHA mismatch deletes the file and fails rather than running the wrong weights.
/// </summary>
public sealed class GgufModelCache(IHttpDownloader downloader, string? root = null) : IGgufModelCache
{
    public string Root { get; } = root
        ?? Environment.GetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_MODEL_ROOT")
        ?? Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "agent-up",
            "models");

    public async Task<string> EnsureAsync(GgufModelSpec spec, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Root);
        var path = Path.Join(Root, spec.FileName);
        if (File.Exists(path))
        {
            if (Hash(path).Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase))
                return path;
            File.Delete(path);
            throw new InvalidOperationException(
                $"Cached GGUF '{spec.FileName}' did not match SHA-256 {spec.Sha256} and was removed.");
        }

        await downloader.DownloadAsync(new Uri(spec.SourceUrl), path, cancellationToken);
        var actual = Hash(path);
        if (actual.Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase))
            return path;

        File.Delete(path);
        throw new InvalidOperationException(
            $"Downloaded GGUF '{spec.FileName}' hashed {actual}, expected {spec.Sha256}.");
    }

    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
