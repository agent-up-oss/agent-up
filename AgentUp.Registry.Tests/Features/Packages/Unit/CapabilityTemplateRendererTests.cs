using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.Packages.Unit;

[TestFixture]
public sealed class CapabilityTemplateRendererTests
{
    [Test]
    public void Render_substitutes_parameter_tokens()
    {
        var rendered = new CapabilityTemplateRenderer().Render(
            "{{parameters.project}}",
            RegistryDomain.DotnetValues("src/Api.csproj"));

        Assert.That(rendered, Is.EqualTo("src/Api.csproj"));
    }

    [Test]
    public void Render_rejects_unknown_tokens()
    {
        var renderer = new CapabilityTemplateRenderer();

        Assert.That(
            () => renderer.Render("{{parameters.missing}}", RegistryDomain.DotnetValues()),
            Throws.InvalidOperationException.With.Message.Contains("parameters.missing"));
    }
}
