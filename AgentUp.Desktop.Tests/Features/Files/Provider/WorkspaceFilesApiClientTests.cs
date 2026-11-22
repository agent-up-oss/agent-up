using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Files.Provider;

[TestFixture]
public sealed class WorkspaceFilesApiClientTests
{
    [Test]
    public async Task GetDirectory_readsOneFolder()
    {
        using var handler = new FakeHttpMessageHandler([]);
        using var http = Http(handler);

        var listing = await new WorkspaceFilesApiClient(http).GetDirectoryAsync(DesktopDomain.WorkspaceId, "apps/storefront");

        Assert.That(listing!.Entries.Single().Path, Is.EqualTo(DesktopDomain.FileProductGrid));
        Assert.That(listing.Entries.Single().Status, Is.EqualTo("Modified"));
        Assert.That(handler.WorkspaceFiles.ListedFolders, Is.EqualTo(new[] { "apps/storefront" }));
    }

    [Test]
    public async Task GetFile_readsTheWholeFileAndWhatTheServerSaysAboutIt()
    {
        using var handler = new FakeHttpMessageHandler([]);
        using var http = Http(handler);
        var client = new WorkspaceFilesApiClient(http);

        var listing = await client.GetDirectoryAsync(DesktopDomain.WorkspaceId, "");
        var file = await client.GetFileAsync(DesktopDomain.WorkspaceId, DesktopDomain.FileReadme);

        Assert.That(listing!.WorkspaceId, Is.EqualTo("ws"));
        Assert.That(listing.Path, Is.Empty);
        Assert.That(file!.Size, Is.EqualTo("# Harbor Shop\n".Length));
        Assert.That(file.IsBinary, Is.False);
        Assert.That(file.IsTooLarge, Is.False);
    }

    [Test]
    public async Task GetFile_isNullWhenTheFileIsGone()
    {
        using var handler = new FakeHttpMessageHandler([]);
        using var http = Http(handler);

        Assert.That(await new WorkspaceFilesApiClient(http).GetFileAsync(DesktopDomain.WorkspaceId, "missing.md"), Is.Null);
    }

    [Test]
    public async Task SaveFile_returnsTheSavedFile()
    {
        using var handler = new FakeHttpMessageHandler([]);
        using var http = Http(handler);
        var client = new WorkspaceFilesApiClient(http);
        var opened = await client.GetFileAsync(DesktopDomain.WorkspaceId, DesktopDomain.FileReadme);

        var saved = await client.SaveFileAsync(DesktopDomain.WorkspaceId, new WorkspaceFileWriteRequestDto(DesktopDomain.FileReadme, "# Edited\n", opened!.Version));

        Assert.That(saved.Saved!.Content, Is.EqualTo("# Edited\n"));
        Assert.That(handler.WorkspaceFiles.Saves, Is.EqualTo(1));
    }

    [Test]
    public async Task SaveFile_reportsAStaleVersionAsAConflict()
    {
        using var handler = new FakeHttpMessageHandler([]);
        using var http = Http(handler);

        var saved = await new WorkspaceFilesApiClient(http).SaveFileAsync(
            DesktopDomain.WorkspaceId, new WorkspaceFileWriteRequestDto(DesktopDomain.FileReadme, "mine", "stale"));

        Assert.That(saved.Conflict, Is.True);
        Assert.That(saved.Error, Does.Contain("changed on the Server"));
    }

    [Test]
    public void GetDirectory_failsWithTheProblemDetail()
    {
        using var http = new HttpClient(new FixedResponse(System.Net.HttpStatusCode.BadRequest, """{"title":"Invalid file request","detail":"'../x' must stay inside the workspace folder."}""")) { BaseAddress = new Uri("http://localhost:5000") };

        Assert.That(
            async () => await new WorkspaceFilesApiClient(http).GetDirectoryAsync(DesktopDomain.WorkspaceId, "../x"),
            Throws.TypeOf<HttpRequestException>().With.Message.EqualTo("'../x' must stay inside the workspace folder."));
    }

    [Test]
    public async Task SaveFile_namesTheStatusWhenTheBodyIsNotAProblem()
    {
        using var http = new HttpClient(new FixedResponse(System.Net.HttpStatusCode.BadGateway, "<html>gateway</html>")) { BaseAddress = new Uri("http://localhost:5000") };

        var saved = await new WorkspaceFilesApiClient(http).SaveFileAsync(DesktopDomain.WorkspaceId, new WorkspaceFileWriteRequestDto(DesktopDomain.FileReadme, "x", "v"));

        Assert.That(saved.Error, Is.EqualTo("HTTP 502"));
        Assert.That(saved.Conflict, Is.False);
    }

    [Test]
    public async Task SaveFile_doesNotTakeABusyAgentForAStaleFile()
    {
        using var http = new HttpClient(new FixedResponse(System.Net.HttpStatusCode.Conflict, """{"title":"Workspace agent is working","detail":"The workspace agent is working."}""")) { BaseAddress = new Uri("http://localhost:5000") };

        var saved = await new WorkspaceFilesApiClient(http).SaveFileAsync(DesktopDomain.WorkspaceId, new WorkspaceFileWriteRequestDto(DesktopDomain.FileReadme, "x", "v"));

        Assert.That(saved.Conflict, Is.False);
        Assert.That(saved.Error, Is.EqualTo("The workspace agent is working."));
    }

    private sealed class FixedResponse(System.Net.HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private static HttpClient Http(FakeHttpMessageHandler handler) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5000") };
}
