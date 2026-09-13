using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class FileServerConnectionStoreTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), $"agent-up-connections-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void Load_returnsEmptySelectionWhenFileIsMissing()
    {
        var store = new FileServerConnectionStore(Path.Join(_root, "connections.json"));

        var selection = store.Load();

        Assert.Multiple(() =>
        {
            Assert.That(selection.Servers, Is.Empty);
            Assert.That(selection.ActiveServerId, Is.Null);
        });
    }

    [Test]
    public void Save_persistsServersAndActiveId()
    {
        var path = Path.Join(_root, "nested", "connections.json");
        var store = new FileServerConnectionStore(path);

        store.Save(new ServerSelection
        {
            ActiveServerId = "one",
            Servers =
            [
                new ConfiguredServer { Id = "one", Url = "http://localhost:5000", AccessToken = "token-1" },
                new ConfiguredServer { Id = "two", Url = "https://agent-up.example.com" }
            ]
        });

        var loaded = store.Load();
        Assert.Multiple(() =>
        {
            Assert.That(loaded.ActiveServerId, Is.EqualTo("one"));
            Assert.That(loaded.Servers, Has.Count.EqualTo(2));
            Assert.That(loaded.Servers[0].AccessToken, Is.EqualTo("token-1"));
            Assert.That(loaded.Servers[1].Url, Is.EqualTo("https://agent-up.example.com"));
        });
    }

    [Test]
    public void Load_ignoresInvalidJson()
    {
        var path = Path.Join(_root, "connections.json");
        File.WriteAllText(path, "{not json");
        var store = new FileServerConnectionStore(path);

        var selection = store.Load();

        Assert.That(selection.Servers, Is.Empty);
    }

    [Test]
    public void Load_dropsServersWithoutIdOrUrlAndRepointsActiveId()
    {
        var path = Path.Join(_root, "connections.json");
        File.WriteAllText(path, """{"ActiveServerId":"missing","Servers":[{"Id":"","Url":"http://localhost:5000"},{"Id":"ok","Url":"https://agent-up.example.com"}]}""");
        var store = new FileServerConnectionStore(path);

        var selection = store.Load();

        Assert.Multiple(() =>
        {
            Assert.That(selection.Servers, Has.Count.EqualTo(1));
            Assert.That(selection.Servers[0].Id, Is.EqualTo("ok"));
            Assert.That(selection.ActiveServerId, Is.EqualTo("ok"));
        });
    }

    [Test]
    public void DefaultConstructor_loadsFromLocalApplicationData()
    {
        var store = new FileServerConnectionStore();

        Assert.That(store.Load(), Is.Not.Null);
    }

    [Test]
    public void Save_throwsWhenThePathHasNoDirectory()
    {
        var store = new FileServerConnectionStore("connections.json");

        Assert.That(
            () => store.Save(new ServerSelection()),
            Throws.InvalidOperationException.With.Message.EqualTo("Connection store path must include a directory."));
    }

    [Test]
    public void Load_returnsEmptySelectionWhenTheFileCannotBeRead()
    {
        var path = Path.Join(_root, "connections.json");
        using (var locked = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            locked.Write("""{"ActiveServerId":"one","Servers":[{"Id":"one","Url":"http://localhost:5000"}]}"""u8);
            locked.Flush();
            var store = new FileServerConnectionStore(path);

            var selection = store.Load();

            Assert.That(selection.Servers, Is.Empty);
        }
    }

    [Test]
    public void Save_deletesTheTemporaryFileWhenTheReplaceFails()
    {
        var path = Path.Join(_root, "connections.json");
        Directory.CreateDirectory(path);
        var store = new FileServerConnectionStore(path);

        Assert.That(
            () => store.Save(new ServerSelection
            {
                Servers = [new ConfiguredServer { Id = "one", Url = "http://localhost:5000" }]
            }),
            Throws.InstanceOf<IOException>());
        Assert.That(Directory.GetFiles(_root, "*.tmp"), Is.Empty);
    }
}
