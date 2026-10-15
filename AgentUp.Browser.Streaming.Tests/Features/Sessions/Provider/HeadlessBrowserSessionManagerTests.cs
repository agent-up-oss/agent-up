using AgentUp.Browser.Streaming;
using AgentUp.Browser.Streaming.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Browser.Streaming.Tests.Features.Sessions.Provider;

[TestFixture]
public sealed class HeadlessBrowserSessionManagerTests
{
    [Test]
    public async Task DeleteWorkspaceProfileAsync_removesTheProfileDirectoryForAGuidWorkspace()
    {
        var workspaceId = Guid.NewGuid().ToString();
        var (session, profiles) = CreateManager();
        try
        {
            var profile = Path.Join(profiles, workspaceId);
            Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Join(profile, "Preferences"), "{}");

            await session.DeleteWorkspaceProfileAsync(workspaceId);

            Assert.That(Directory.Exists(profile), Is.False);
        }
        finally
        {
            DeleteTree(profiles);
        }
    }

    [Test]
    public async Task DeleteWorkspaceProfileAsync_leavesNonGuidIdentifiersUntouched()
    {
        var (session, profiles) = CreateManager();
        try
        {
            var outside = Path.Join(profiles, "not-a-guid");
            Directory.CreateDirectory(outside);

            await session.DeleteWorkspaceProfileAsync("../secret");
            await session.DeleteWorkspaceProfileAsync("not-a-guid");

            Assert.That(Directory.Exists(outside), Is.True);
        }
        finally
        {
            DeleteTree(profiles);
        }
    }

    [Test]
    public async Task DeleteWorkspaceProfileAsync_isANoOpWhenTheProfileIsMissing()
    {
        var (session, profiles) = CreateManager();
        try
        {
            Assert.DoesNotThrowAsync(() => session.DeleteWorkspaceProfileAsync(Guid.NewGuid().ToString()));
        }
        finally
        {
            DeleteTree(profiles);
        }
    }

    private static (HeadlessBrowserSessionManager Session, string Profiles) CreateManager()
    {
        var profiles = Path.Join(Path.GetTempPath(), "agent-up-browser-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profiles);
        var chromium = Path.Join(profiles, "chromium");
        Directory.CreateDirectory(chromium);
        var session = new HeadlessBrowserSessionManager(
            chromium,
            profiles,
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new FakeStreamSessionEventSink(),
            NullLogger<HeadlessBrowserSessionManager>.Instance);
        return (session, profiles);
    }

    private static void DeleteTree(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
