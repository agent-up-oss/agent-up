using AgentUp.Desktop.Features.Applications.Controllers;
using AgentUp.Desktop.Features.Applications.Services;
using AgentUp.Desktop.Features.Applications.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Applications.Controller;

[TestFixture]
public sealed class ApplicationsControllerTests
{
    [Test]
    public void Normalize_preserves_application_order_and_identity()
    {
        var first = new ApplicationViewModel("web", "npm start", "running");
        var second = new ApplicationViewModel("api", "dotnet run", "stopped");

        var result = new ApplicationsController(new ApplicationSelectionService()).Normalize([first, second]);

        Assert.That(result, Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void Normalize_accepts_an_empty_workspace()
        => Assert.That(new ApplicationsController(new ApplicationSelectionService()).Normalize([]), Is.Empty);
}
