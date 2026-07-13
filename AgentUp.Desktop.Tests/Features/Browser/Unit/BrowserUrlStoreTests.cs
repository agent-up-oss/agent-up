using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserUrlStoreTests
{
    private string _testRoot = null!;
    private string _savedRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"agentup-url-test-{Guid.NewGuid()}");
        _savedRoot = BrowserUrlStore.RootPath;
        BrowserUrlStore.RootPath = _testRoot;
    }

    [TearDown]
    public void TearDown()
    {
        BrowserUrlStore.RootPath = _savedRoot;
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Test]
    public void Read_returnsNull_whenNoFileExists()
    {
        var result = BrowserUrlStore.Read("ws-1", "http://localhost:3000/");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Write_thenRead_returnsSavedUrl_whenPortMatches()
    {
        BrowserUrlStore.Write("ws-1", "http://localhost:3000/dashboard");

        var result = BrowserUrlStore.Read("ws-1", "http://localhost:3000/");

        Assert.That(result, Is.EqualTo("http://localhost:3000/dashboard"));
    }

    [Test]
    public void Read_returnsNull_whenSavedPortDiffersFromBaseUrl()
    {
        BrowserUrlStore.Write("ws-1", "http://localhost:3000/dashboard");

        var result = BrowserUrlStore.Read("ws-1", "http://localhost:4000/");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Read_returnsNull_whenSavedFileContainsInvalidUrl()
    {
        var dir = BrowserUrlStore.ProfilePath("ws-1");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "last-url.txt"), "not-a-url");

        var result = BrowserUrlStore.Read("ws-1", "http://localhost:3000/");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Write_overwritesPreviouslySavedUrl()
    {
        BrowserUrlStore.Write("ws-1", "http://localhost:3000/old");
        BrowserUrlStore.Write("ws-1", "http://localhost:3000/new");

        var result = BrowserUrlStore.Read("ws-1", "http://localhost:3000/");

        Assert.That(result, Is.EqualTo("http://localhost:3000/new"));
    }

    [Test]
    public void Read_isolatesUrlsByWorkspaceId()
    {
        BrowserUrlStore.Write("ws-1", "http://localhost:3000/ws1-page");
        BrowserUrlStore.Write("ws-2", "http://localhost:3000/ws2-page");

        Assert.That(BrowserUrlStore.Read("ws-1", "http://localhost:3000/"), Is.EqualTo("http://localhost:3000/ws1-page"));
        Assert.That(BrowserUrlStore.Read("ws-2", "http://localhost:3000/"), Is.EqualTo("http://localhost:3000/ws2-page"));
    }
}
