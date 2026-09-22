using AgentUp.Sdk.Common;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Sdk.Runtime.Tests.Features.RuntimeSdk.Unit;

[TestFixture]
public sealed class RuntimeCapabilityProjectDefinitionTests
{
    [Test]
    public void CreateProject_registers_the_runtime_implementation()
    {
        var project = new SampleRuntimeProject().CreateProject();

        Assert.That(project.Registrations.Single().ImplementationType, Is.EqualTo(typeof(SampleRuntimeCapability)));
    }

    [Test]
    public void Builder_can_register_more_than_one_implementation()
    {
        var builder = new RuntimeCapabilityRegistrationBuilder();
        builder.Add<SampleRuntimeCapability>().Add<SecondRuntimeCapability>();

        Assert.That(builder.Registrations, Has.Count.EqualTo(2));
    }

    private sealed class SampleRuntimeProject : RuntimeCapabilityProjectDefinition
    {
        protected override void Register(RuntimeCapabilityRegistrationBuilder registry)
            => registry.Add<SampleRuntimeCapability>();
    }

    private class SampleRuntimeCapability : IRuntimeCapability
    {
        public CapabilityIdentity Identity { get; } = new("sample", "1.0.0", "Sample", "agent-up");
        public string SectionName => "sample";
        public IReadOnlyList<NixPackageDeclaration> NixPackages => [];
        public IReadOnlyList<RuntimeAttributeSpec> ExtraAttributes => [];

        public RuntimeBindResult Bind(IReadOnlyList<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> items)
            => new(true, [], []);

        public RuntimeDeliverResult Deliver(string? technologyVersion) => new(true, null, []);

        public RuntimeHostResult Host(RuntimeHostRequest app) => new(true, "sample", [], []);
    }

    private sealed class SecondRuntimeCapability : SampleRuntimeCapability;
}
