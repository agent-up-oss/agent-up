using AgentUp.CLI.Features.Capabilities.Providers;

namespace AgentUp.CLI.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityPackProcessRunnerTests
{
    [Test]
    public void RunAsync_rejectsACommandOutsideTheAllowlist()
    {
        var runner = new CapabilityPackProcessRunner();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync("python", ["-c", "print(1)"], "/tmp"));

        Assert.That(ex!.Message, Does.Contain("allowlisted"));
    }

    [Test]
    public void Allowed_names_are_only_bash()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => new CapabilityPackProcessRunner().RunAsync("dotnet", ["run"], "/tmp"));
    }

    [Test]
    public async Task RunAsync_executesAnAllowlistedBashCommand()
    {
        var result = await new CapabilityPackProcessRunner().RunAsync("bash", ["-c", "echo packed"], "/tmp");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardOutput, Does.Contain("packed"));
        });
    }

    [Test]
    [CancelAfter(10_000)]
    public async Task RunAsync_drainsStdoutAndStderrConcurrently()
    {
        var result = await new CapabilityPackProcessRunner().RunAsync(
            "bash",
            ["-c", "printf 'out\\n'; i=0; while [ \"$i\" -lt 40000 ]; do printf 'xxxxxxxx\\n' >&2; i=$((i+1)); done; printf 'done\\n'"],
            "/tmp");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardOutput, Does.Contain("done"));
            Assert.That(result.StandardError.Length, Is.GreaterThan(64 * 1024));
        });
    }
}
