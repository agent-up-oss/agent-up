using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Agent.Tests.Features.AgentSdk.Unit;

[TestFixture]
public sealed class AgentCapabilityProjectDefinitionTests
{
    [Test]
    public void CreateProject_registers_the_agent_implementation()
    {
        var project = new SampleAgentProject().CreateProject();

        Assert.That(project.Registrations.Single().ImplementationType, Is.EqualTo(typeof(SampleAgentCapability)));
    }

    [Test]
    public void Launch_returns_the_module_owned_stdio_command()
    {
        var launch = new SampleAgentCapability().Launch();

        Assert.That(launch.FileName, Is.EqualTo("sample-acp"));
        Assert.That(launch.Arguments, Is.Empty);
    }

    private sealed class SampleAgentProject : AgentCapabilityProjectDefinition
    {
        protected override void Register(AgentCapabilityRegistrationBuilder registry)
            => registry.Add<SampleAgentCapability>();
    }

    private sealed class SampleAgentCapability : IAgentCapability
    {
        public CapabilityIdentity Identity { get; } = new("sample", "1.0.0", "Sample", "agent-up");
        public IReadOnlyList<NixPackageDeclaration> NixPackages { get; } = [new("nodejs_22")];
        public bool CanRun => true;
        public AgentLoginSpec? Login => new("sample", ["login"], "poll");

        public AgentLaunchResult Launch() => new("sample-acp", []);
    }
}
