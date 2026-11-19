using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentSandboxLayoutTests
{
    private const string Clone = "/work/clones/author-1";
    private const string Git = "/work/clones/author-1/.git";

    [Test]
    public void Mounts_giveAnAuthorAWritableCloneButNeverItsGitDirectory()
    {
        var mounts = AgentSandboxLayout.Mounts(Inputs(), TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(mounts.Single(mount => mount.Path == Clone).Writable, Is.True);
            Assert.That(mounts.Single(mount => mount.Path == Git).Writable, Is.False);
            Assert.That(IndexOf(mounts, Git), Is.GreaterThan(IndexOf(mounts, Clone)),
                "the Git directory is bound after the clone so the read-only bind wins");
        });
    }

    [Test]
    public void Mounts_giveADevReviewerAReadOnlyClone()
    {
        var mounts = AgentSandboxLayout.Mounts(Inputs(), TeamMemberGrants.DevReviewer);

        Assert.That(mounts.Single(mount => mount.Path == Clone).Writable, Is.False);
    }

    [Test]
    public void Mounts_refuseAPmReviewerWhichHasNoClone()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => AgentSandboxLayout.Mounts(Inputs(), TeamMemberGrants.PmReviewer));

        Assert.That(refused!.Message, Does.Contain("no clone"));
    }

    [Test]
    public void Mounts_keepToolsReadOnlyAndOnlyTheAgentHomeWritable()
    {
        var mounts = AgentSandboxLayout.Mounts(Inputs(), TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(mounts.Where(mount => mount.Path is "/usr" or "/etc" or "/tools/bin").Select(mount => mount.Writable),
                Is.All.False);
            Assert.That(mounts.Single(mount => mount.Path == "/data/agent-cli-home").Writable, Is.True);
            Assert.That(mounts.Select(mount => mount.Path), Does.Not.Contain("/data"),
                "the Server data directory itself is never mounted");
        });
    }

    [Test]
    public void Mounts_shareTheNixDaemonWhenTheStoreIsReadOnly()
    {
        var mounts = AgentSandboxLayout.Mounts(Inputs(nixWritable: false), TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(mounts.Single(mount => mount.Path == "/nix").Writable, Is.False);
            Assert.That(mounts.Single(mount => mount.Path == "/nix/var/nix/daemon-socket").Writable, Is.True);
        });
    }

    [Test]
    public void Mounts_letASingleUserNixWriteItsStore()
    {
        var mounts = AgentSandboxLayout.Mounts(Inputs(nixWritable: true), TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(mounts.Single(mount => mount.Path == "/nix").Writable, Is.True);
            Assert.That(mounts.Select(mount => mount.Path), Does.Not.Contain("/nix/var/nix/daemon-socket"));
        });
    }

    [Test]
    public void Environment_dropsTheServersSecretsAndKeepsWhatTheAgentIsGiven()
    {
        var server = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
            ["AGENTUP_ADMIN_PASSWORD"] = "secret",
            ["AGENTUP_EXTERNAL_SIGNING_KEY"] = "key",
            ["HTTPS_PROXY"] = "http://proxy"
        };
        var agent = new Dictionary<string, string> { ["HOME"] = "/data/agent-cli-home", ["PATH"] = "/tools/bin:/usr/bin" };

        var environment = AgentSandboxLayout.Environment(server, agent);

        Assert.Multiple(() =>
        {
            Assert.That(environment.Keys, Does.Not.Contain("AGENTUP_ADMIN_PASSWORD").And.Not.Contain("AGENTUP_EXTERNAL_SIGNING_KEY"));
            Assert.That(environment["PATH"], Is.EqualTo("/tools/bin:/usr/bin"), "the agent's own PATH wins");
            Assert.That(environment["HOME"], Is.EqualTo("/data/agent-cli-home"));
            Assert.That(environment["HTTPS_PROXY"], Is.EqualTo("http://proxy"));
            Assert.That(environment["TMPDIR"], Is.EqualTo("/tmp"));
        });
    }

    private static int IndexOf(IReadOnlyList<AgentSandboxMount> mounts, string path) =>
        mounts.Select((mount, index) => (mount, index)).Single(pair => pair.mount.Path == path).index;

    private static AgentSandboxInputs Inputs(bool nixWritable = false) => new(
        ["/usr", "/etc"],
        "/nix",
        nixWritable,
        "/nix/var/nix/daemon-socket",
        ["/tools/bin", "/data/capabilities/nix"],
        "/data/agent-cli-home",
        Clone,
        [Git]);
}
