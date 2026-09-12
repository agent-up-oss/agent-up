using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class HostSessionStoreTests
{
    [Test]
    public void WriteReadDelete_roundTripsSession()
    {
        var root = CreateTempRoot();
        var store = new HostSessionStore(new DebugPathValidator(root));
        var session = new HostSessionDto(9, root, Path.Join(root, ".git", "agent-up", "au-debug"), [
            new HostedProcessDto("server", 44, Path.Join(root, ".git", "agent-up", "au-debug", "logs", "server.log"), DebugLayout.ServerUrl)
        ]);

        store.Write(session);
        var loaded = store.Read();
        store.Delete();

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.SupervisorPid, Is.EqualTo(9));
            Assert.That(loaded.Processes[0].Name, Is.EqualTo("server"));
            Assert.That(store.Read(), Is.Null);
        });
    }

    [Test]
    public void ScreenshotPath_staysUnderSession()
    {
        var root = CreateTempRoot();
        var paths = new DebugPathValidator(root);
        var path = new HostSessionStore(paths).ScreenshotPath("desktop");

        Assert.That(path, Does.StartWith(paths.ScreenshotsDirectory));
        Assert.That(path, Does.EndWith(".png"));
    }

    [Test]
    public void ReadLogTail_returnsLastLines()
    {
        var root = CreateTempRoot();
        var paths = new DebugPathValidator(root);
        Directory.CreateDirectory(paths.LogsDirectory);
        var log = Path.Join(paths.LogsDirectory, "server.log");
        File.WriteAllLines(log, ["a", "b", "c", "d"]);

        var tail = new HostSessionStore(paths).ReadLogTail(log, 2);

        Assert.That(tail, Is.EqualTo($"c{Environment.NewLine}d"));
    }

    [Test]
    public void ReadLogTail_missingFile_isEmpty()
    {
        var root = CreateTempRoot();
        var paths = new DebugPathValidator(root);
        var log = Path.Join(paths.LogsDirectory, "missing.log");

        Assert.That(new HostSessionStore(paths).ReadLogTail(log, 4), Is.EqualTo(string.Empty));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-session", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        return root;
    }
}
