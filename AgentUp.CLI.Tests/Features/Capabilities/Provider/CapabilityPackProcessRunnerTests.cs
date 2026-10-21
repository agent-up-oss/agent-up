using AgentUp.CLI.Features.Capabilities.Providers;

namespace AgentUp.CLI.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityPackProcessRunnerTests
{
    [Test]
    public void Run_rejectsACommandOutsideTheAllowlist()
    {
        var runner = new CapabilityPackProcessRunner();

        Assert.That(
            () => runner.Run("python", ["-c", "print(1)"], "/tmp"),
            Throws.InvalidOperationException.With.Message.Contains("allowlisted"));
    }

    [Test]
    public void Allowed_names_are_only_bash()
    {
        Assert.That(
            () => new CapabilityPackProcessRunner().Run("dotnet", ["run"], "/tmp"),
            Throws.InvalidOperationException);
    }

    [Test]
    public void Run_executesAnAllowlistedBashCommand()
    {
        var result = new CapabilityPackProcessRunner().Run("bash", ["-c", "echo packed"], "/tmp");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardOutput, Does.Contain("packed"));
        });
    }
}
