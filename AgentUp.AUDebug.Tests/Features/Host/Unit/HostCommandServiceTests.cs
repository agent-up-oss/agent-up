using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Services;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Host.Unit;

[TestFixture]
public sealed class HostCommandServiceTests
{
    [Test]
    public async Task Up_whenAlreadyRunning_doesNotStartAgain()
    {
        var supervisor = new FakeSupervisor { Live = true };
        var sessions = new FakeSessionStore { Session = supervisor.Session };
        var service = Service(sessions, supervisor, new FakeReadyProbe());

        var result = await service.UpAsync(Command(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Message, Does.Contain("already running"));
            Assert.That(supervisor.Starts, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Up_detach_startsAndReturnsReady()
    {
        var supervisor = new FakeSupervisor();
        var probe = new FakeReadyProbe();
        var windows = new FakeDesktopWindowDriver();
        var service = Service(new FakeSessionStore(), supervisor, probe, windows);

        var result = await service.UpAsync(Command(detach: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Message, Does.Contain("au-debug ready"));
            Assert.That(supervisor.Starts, Is.EqualTo(1));
            Assert.That(supervisor.Waits, Is.EqualTo(0));
            Assert.That(probe.Urls, Has.Count.EqualTo(3));
            Assert.That(windows.Waits, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Up_timeout_stopsStartedSession()
    {
        var supervisor = new FakeSupervisor();
        var probe = new FakeReadyProbe { DelayUntilCanceled = true };
        var service = Service(new FakeSessionStore(), supervisor, probe);

        var result = await service.UpAsync(Command(timeout: TimeSpan.FromMilliseconds(40), detach: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("Timed out"));
            Assert.That(supervisor.Stops, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Down_stopsLiveSession()
    {
        var supervisor = new FakeSupervisor { Live = true };
        var sessions = new FakeSessionStore { Session = supervisor.Session };
        var service = Service(sessions, supervisor, new FakeReadyProbe());

        var result = await service.DownAsync(Command(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Message, Does.Contain("Stopped desktop, mobile, docs"));
            Assert.That(supervisor.Stops, Is.EqualTo(1));
            Assert.That(sessions.Session, Is.Null);
        });
    }

    [Test]
    public async Task Down_whenMissing_returnsIdle()
    {
        var result = await Service(new FakeSessionStore(), new FakeSupervisor(), new FakeReadyProbe())
            .DownAsync(Command(), CancellationToken.None);

        Assert.That(result.Message, Does.Contain("not running"));
    }

    [Test]
    public async Task Status_whenMissing_fails()
    {
        var result = await Service(new FakeSessionStore(), new FakeSupervisor(), new FakeReadyProbe())
            .StatusAsync(Command(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("not running"));
        });
    }

    [Test]
    public async Task Status_whenReady_reportsEachSurface()
    {
        var supervisor = new FakeSupervisor { Live = true };
        var windows = new FakeDesktopWindowDriver { WindowPresent = true };
        var service = Service(new FakeSessionStore { Session = supervisor.Session }, supervisor, new FakeReadyProbe(), windows);

        var result = await service.StatusAsync(Command(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Message, Does.Contain("au-debug running"));
            Assert.That(result.Message, Does.Contain("server: http://127.0.0.1:5001 ready"));
            Assert.That(result.Message, Does.Contain("desktop: window AgentUp.Desktop present"));
            Assert.That(result.Message, Does.Contain("mobile: http://127.0.0.1:10102 ready"));
            Assert.That(result.Message, Does.Contain("docs: http://127.0.0.1:10100 ready"));
        });
    }

    [Test]
    public async Task Status_whenDesktopMissing_fails()
    {
        var supervisor = new FakeSupervisor { Live = true };
        var windows = new FakeDesktopWindowDriver { WindowPresent = false };
        var service = Service(new FakeSessionStore { Session = supervisor.Session }, supervisor, new FakeReadyProbe(), windows);

        var result = await service.StatusAsync(Command(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("desktop: window AgentUp.Desktop missing"));
        });
    }

    private static HostCommandService Service(
        FakeSessionStore sessions,
        FakeSupervisor supervisor,
        FakeReadyProbe probe,
        FakeDesktopWindowDriver? windows = null)
        => new(sessions, supervisor, probe, windows ?? new FakeDesktopWindowDriver(), new DebugOutputService(new StringWriter()));

    private static DebugCommandDto Command(TimeSpan? timeout = null, bool detach = false)
        => new("up", null, null, null, null, timeout ?? TimeSpan.FromSeconds(30), detach);
}
