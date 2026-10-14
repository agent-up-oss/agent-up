using AgentUp.TestAgents.Features.Inference.Interfaces;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>HTTP GET of a GGUF onto disk. A cache miss is slow; a failed fetch is an error.</summary>
public sealed class HttpDownloader : IHttpDownloader
{
    private readonly HttpClient _client;

    public HttpDownloader(HttpMessageHandler? handler = null)
    {
        _client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _client.Timeout = TimeSpan.FromMinutes(10);
    }

    public async Task DownloadAsync(Uri source, string destination, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var response = await _client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var staging = destination + ".partial";
        await using (var file = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None))
            await response.Content.CopyToAsync(file, cancellationToken);
        File.Move(staging, destination, overwrite: true);
    }
}
