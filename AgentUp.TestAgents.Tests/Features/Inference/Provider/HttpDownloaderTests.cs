using System.Net;
using System.Net.Http.Headers;
using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class HttpDownloaderTests
{
    [Test]
    public async Task DownloadAsync_writesTheResponseBodyToTheDestination()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-http-gguf").FullName;
        try
        {
            var destination = Path.Join(root, "model.gguf");
            var handler = new StubHandler(new ByteArrayContent("gguf"u8.ToArray()));
            var downloader = new HttpDownloader(handler);

            await downloader.DownloadAsync(new Uri("https://example.test/model.gguf"), destination, CancellationToken.None);

            Assert.That(await File.ReadAllTextAsync(destination), Is.EqualTo("gguf"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void DownloadAsync_failsWhenTheMirrorRejectsTheFetch()
    {
        var root = Directory.CreateTempSubdirectory("agent-up-http-gguf").FullName;
        try
        {
            var destination = Path.Join(root, "model.gguf");
            var handler = new StubHandler(new ByteArrayContent([]), HttpStatusCode.NotFound);
            var downloader = new HttpDownloader(handler);

            Assert.That(
                async () => await downloader.DownloadAsync(
                    new Uri("https://example.test/missing.gguf"),
                    destination,
                    CancellationToken.None),
                Throws.TypeOf<HttpRequestException>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubHandler(HttpContent content, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = content
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(response);
        }
    }
}
