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

    private static HttpClient Http(FakeHttpMessageHandler handler) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5000") };
}
