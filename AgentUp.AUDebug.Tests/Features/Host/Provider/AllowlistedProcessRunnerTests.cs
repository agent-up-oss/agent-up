using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class AllowlistedProcessRunnerTests
{
    [Test]
    public void Start_rejectsUnknownExecutable()
    {
        var runner = new AllowlistedProcessRunner();
        Assert.That(
            () => runner.Start(new AllowlistedCommand("rm", ["-rf", "/"], Directory.GetCurrentDirectory())),
            Throws.InvalidOperationException.With.Message.Contains("not allowlisted"));
    }

    [Test]
    public async Task RunAsync_executesTrue()
    {
        var runner = new AllowlistedProcessRunner();
        var result = await runner.RunAsync(
            new AllowlistedCommand("true", [], Directory.GetCurrentDirectory()),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
    }

    [Test]
    public void IsRunning_unknownPid_isFalse()
    {
        Assert.That(new AllowlistedProcessRunner().IsRunning(int.MaxValue), Is.False);
    }

    [Test]
    public void KillTree_unknownPid_doesNotThrow()
    {
        Assert.DoesNotThrow(() => new AllowlistedProcessRunner().KillTree(int.MaxValue));
    }

    [Test]
    public async Task RunAsync_writesStandardInput()
    {
        var result = await new AllowlistedProcessRunner().RunAsync(
            new AllowlistedCommand("bash", ["-c", "cat"], Directory.GetCurrentDirectory(), StandardInput: "hello"),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardOutput, Is.EqualTo("hello"));
    }

    [Test]
    public async Task RunAsync_forwardsEnvironment()
    {
        var result = await new AllowlistedProcessRunner().RunAsync(
            new AllowlistedCommand(
                "bash",
                ["-c", "printf %s \"$AU_DEBUG_TEST\""],
                Directory.GetCurrentDirectory(),
                new Dictionary<string, string> { ["AU_DEBUG_TEST"] = "from-env" }),
            CancellationToken.None);

        Assert.That(result.StandardOutput, Is.EqualTo("from-env"));
    }

    [Test]
    public void RunAsync_cancel_killsTheProcess()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.That(
            async () => await new AllowlistedProcessRunner().RunAsync(
                new AllowlistedCommand("bash", ["-c", "sleep 10"], Directory.GetCurrentDirectory()),
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
