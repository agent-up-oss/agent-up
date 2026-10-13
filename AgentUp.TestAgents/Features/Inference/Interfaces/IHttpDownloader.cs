namespace AgentUp.TestAgents.Features.Inference.Interfaces;

/// <summary>Fetches a URL onto a local path. Cache misses are slow, not failures.</summary>
public interface IHttpDownloader
{
    Task DownloadAsync(Uri source, string destination, CancellationToken cancellationToken);
}
