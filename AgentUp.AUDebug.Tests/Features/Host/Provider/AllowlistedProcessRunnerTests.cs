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
}
