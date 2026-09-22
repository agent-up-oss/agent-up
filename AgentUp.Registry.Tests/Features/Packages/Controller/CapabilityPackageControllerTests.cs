using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.Packages.Controller;

[TestFixture]
public sealed class CapabilityPackageControllerTests
{
    [Test]
    public void Validate_returns_the_validator_contract()
    {
        var controller = new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer());

        var result = controller.Validate(RegistryDomain.DotnetPackage());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Render_returns_substituted_text()
    {
        var controller = new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer());

        var rendered = controller.Render("{{parameters.project}}", RegistryDomain.DotnetValues("Web.csproj"));

        Assert.That(rendered, Is.EqualTo("Web.csproj"));
    }
}
