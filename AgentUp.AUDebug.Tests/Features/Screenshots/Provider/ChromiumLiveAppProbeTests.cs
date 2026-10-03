using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Shared.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

[TestFixture]
public sealed class ChromiumLiveAppProbeTests
{
    [Test]
    public async Task ReadPageText_usesDumpDom()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        var processes = new FakeProcessRunner { NextResult = new(0, "<p>Harbor Shop</p>", "") };
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var probe = new ChromiumLiveAppProbe(processes, environment, new DebugPathValidator(root));

        var html = await probe.ReadPageTextAsync("http://127.0.0.1:10102/connect", CancellationToken.None);

        Assert.That(html, Does.Contain("Harbor Shop"));
        Assert.That(processes.Ran[0].FileName, Is.EqualTo("chromium"));
        Assert.That(processes.Ran[0].Arguments, Does.Contain("--dump-dom"));
        Assert.That(processes.Ran[0].Arguments, Does.Contain("http://127.0.0.1:10102/connect"));
    }

    [Test]
    public void ReadPageText_reportsChromiumFailure()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        var processes = new FakeProcessRunner { NextResult = new(1, "", "dump exploded") };
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var probe = new ChromiumLiveAppProbe(processes, environment, new DebugPathValidator(root));

        Assert.That(
            async () => await probe.ReadPageTextAsync("http://127.0.0.1:10102/", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("dump exploded"));
    }
}
