using AgentUp.Browser.Streaming;
using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class StreamStateDerivationTests
{
    // ── Chromium precedence (step 1) ──────────────────────────────────

    [TestCase("not_started", 0)]
    [TestCase("downloading", 42)]
    [TestCase("failed", 0)]
    public void Compute_chromiumNotReady_returnsChromiumDownloading(string chromiumState, int progress)
    {
        var result = StreamStateDerivation.Compute(Running(), chromiumState, progress);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.ChromiumDownloading));
        Assert.That(result.ChromiumState, Is.EqualTo(chromiumState));
        Assert.That(result.ChromiumProgress, Is.EqualTo(progress));
    }

    [Test]
    public void Compute_chromiumReady_doesNotShortCircuit()
    {
        var result = StreamStateDerivation.Compute(Stopped(), "ready", 100);

        Assert.That(result.Kind, Is.Not.EqualTo(StreamKind.ChromiumDownloading));
    }

    // ── Workspace lifecycle (step 2) ──────────────────────────────────

    [Test]
    public void Compute_workspaceNotRunning_returnsStopped()
    {
        var result = StreamStateDerivation.Compute(Stopped(), "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.WorkspaceStopped));
    }

    // ── App reachability — no target (step 3) ────────────────────────

    [Test]
    public void Compute_noCurrentTarget_returnsConnectingWithZeroAttempts()
    {
        var result = StreamStateDerivation.Compute(Running(), "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.AppConnecting));
        Assert.That(result.Attempt, Is.EqualTo(0));
        Assert.That(result.MaxAttempts, Is.EqualTo(0));
    }

    // ── App reachability — health-checked target (step 3) ────────────

    [Test]
    public void Compute_healthCheckedTarget_notYetChecked_returnsConnecting()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget("api", 3000, "http://localhost:3000", HealthChecked: true),
            PortHealth = new Dictionary<string, string>(),
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.AppConnecting));
    }

    [Test]
    public void Compute_healthCheckedTarget_unhealthy_returnsConnecting()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget("api", 3000, "http://localhost:3000", HealthChecked: true),
            PortHealth = new Dictionary<string, string>
            {
                [StreamStateDerivation.HealthKey("api", 3000)] = "Unhealthy"
            },
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.AppConnecting));
    }

    [Test]
    public void Compute_healthCheckedTarget_healthy_sessionInactive_returnsSessionLaunching()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget("api", 3000, "http://localhost:3000", HealthChecked: true),
            PortHealth = new Dictionary<string, string>
            {
                [StreamStateDerivation.HealthKey("api", 3000)] = "Healthy"
            },
            SessionActive = false,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.SessionLaunching));
    }

    [Test]
    public void Compute_healthCheckedTarget_healthy_sessionActive_returnsStreaming()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget("api", 3000, "http://localhost:3000", HealthChecked: true),
            PortHealth = new Dictionary<string, string>
            {
                [StreamStateDerivation.HealthKey("api", 3000)] = "Healthy"
            },
            SessionActive = true,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.Streaming));
    }

    // ── App reachability — standalone probe target (step 3) ──────────

    [Test]
    public void Compute_standaloneTarget_probeNotStarted_returnsConnectingAttemptZero()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget(null, 5000, "http://localhost:5000", HealthChecked: false),
            StandaloneProbeAttempt = 0,
            StandaloneProbeReachable = false,
            StandaloneProbeExhausted = false,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.AppConnecting));
        Assert.That(result.Attempt, Is.EqualTo(0));
        Assert.That(result.MaxAttempts, Is.EqualTo(StreamStateDerivation.StandaloneMaxAttempts));
    }

    [Test]
    public void Compute_standaloneTarget_probeInProgress_returnsConnectingWithAttempt()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget(null, 5000, "http://localhost:5000", HealthChecked: false),
            StandaloneProbeAttempt = 7,
            StandaloneProbeReachable = false,
            StandaloneProbeExhausted = false,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.AppConnecting));
        Assert.That(result.Attempt, Is.EqualTo(7));
    }

    [Test]
    public void Compute_standaloneTarget_probeExhausted_returnsFailed()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget(null, 5000, "http://localhost:5000", HealthChecked: false),
            StandaloneProbeAttempt = StreamStateDerivation.StandaloneMaxAttempts,
            StandaloneProbeReachable = false,
            StandaloneProbeExhausted = true,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.AppFailed));
        Assert.That(result.MaxAttempts, Is.EqualTo(StreamStateDerivation.StandaloneMaxAttempts));
    }

    [Test]
    public void Compute_standaloneTarget_probeReachable_sessionInactive_returnsSessionLaunching()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget(null, 5000, "http://localhost:5000", HealthChecked: false),
            StandaloneProbeReachable = true,
            StandaloneProbeExhausted = false,
            SessionActive = false,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.SessionLaunching));
    }

    [Test]
    public void Compute_standaloneTarget_probeReachable_sessionActive_returnsStreaming()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget(null, 5000, "http://localhost:5000", HealthChecked: false),
            StandaloneProbeReachable = true,
            StandaloneProbeExhausted = false,
            SessionActive = true,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.Streaming));
    }

    // ── Chromium precedence overrides all workspace state ─────────────

    [Test]
    public void Compute_chromiumDownloading_overridesRunningWorkspaceWithSession()
    {
        var inputs = Running() with
        {
            CurrentTarget = new CurrentStreamTarget(null, 5000, "http://localhost:5000", HealthChecked: false),
            StandaloneProbeReachable = true,
            SessionActive = true,
        };

        var result = StreamStateDerivation.Compute(inputs, "downloading", 75);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.ChromiumDownloading));
    }

    // ── Stopped workspace overrides app/session signals ───────────────

    [Test]
    public void Compute_workspaceStopped_overridesActiveSession()
    {
        var inputs = new WorkspaceStreamInputs
        {
            IsRunning = false,
            SessionActive = true,
            CurrentTarget = new CurrentStreamTarget(null, 5000, "http://localhost:5000", HealthChecked: false),
            StandaloneProbeReachable = true,
        };

        var result = StreamStateDerivation.Compute(inputs, "ready", 100);

        Assert.That(result.Kind, Is.EqualTo(StreamKind.WorkspaceStopped));
    }

    // ── HealthKey format ──────────────────────────────────────────────

    [Test]
    public void HealthKey_formatsAsAppNameColonPort()
    {
        Assert.That(StreamStateDerivation.HealthKey("frontend", 3000), Is.EqualTo("frontend:3000"));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static WorkspaceStreamInputs Running() => new() { IsRunning = true };
    private static WorkspaceStreamInputs Stopped() => new() { IsRunning = false };
}
