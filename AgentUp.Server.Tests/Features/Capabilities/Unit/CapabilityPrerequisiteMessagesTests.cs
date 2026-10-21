using AgentUp.Server.Features.Capabilities.Models;

namespace AgentUp.Server.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityPrerequisiteMessagesTests
{
    [Test]
    public void NixMissing_names_dotnet_sdk_and_the_install_url()
    {
        var messages = CapabilityPrerequisiteMessages.NixMissing("dotnet");

        Assert.Multiple(() =>
        {
            Assert.That(messages[0], Does.Contain("the .NET SDK"));
            Assert.That(messages, Has.Some.Contains(CapabilityPrerequisiteMessages.NixInstallUrl));
            Assert.That(messages, Has.Some.Contains("WSL2"));
        });
    }

    [Test]
    public void NixMissing_namesDockerAgentCliAndUnknownToolchains()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CapabilityPrerequisiteMessages.NixMissing("docker")[0], Does.Contain("Docker"));
            Assert.That(CapabilityPrerequisiteMessages.NixMissing("codex")[0], Does.Contain("the agent CLI"));
            Assert.That(CapabilityPrerequisiteMessages.NixMissing("python")[0], Does.Contain("its toolchain"));
        });
    }

    [Test]
    public void NotEnabled_names_the_application_and_the_bootstrap_command()
    {
        var messages = CapabilityPrerequisiteMessages.NotEnabled("dotnet", "Example API");

        Assert.Multiple(() =>
        {
            Assert.That(messages[0], Is.EqualTo("Application 'Example API' needs capability 'dotnet', which is not enabled."));
            Assert.That(messages[1], Is.EqualTo($"Run: {CapabilityPrerequisiteMessages.BootstrapCommand}"));
        });
    }

    [Test]
    public void LaunchFailure_keepsCauseAndRemedyOnSeparateIndentedLines()
    {
        var text = CapabilityPrerequisiteMessages.LaunchFailure(
            CapabilityPrerequisiteMessages.NotEnabled("dotnet", "Example API"));

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Example API"));
            Assert.That(text, Does.Contain($"Run: {CapabilityPrerequisiteMessages.BootstrapCommand}"));
            Assert.That(text, Does.Contain("       "));
        });
    }
}
