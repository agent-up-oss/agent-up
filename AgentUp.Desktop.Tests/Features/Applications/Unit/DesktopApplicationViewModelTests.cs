using AgentUp.Desktop.Features.Applications.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Applications.Unit;

[TestFixture]
public sealed class DesktopApplicationViewModelTests
{
    [Test]
    public void Desktop_application_is_explicitly_identified_without_a_port()
    {
        var application = new ApplicationViewModel(
            "Editor", "dotnet run", "Running", allocatedPorts: [], isDesktop: true);

        Assert.Multiple(() =>
        {
            Assert.That(application.IsDesktop, Is.True);
            Assert.That(application.AllocatedPorts, Is.Empty);
            Assert.That(new DesktopSubTabViewModel().Label, Is.EqualTo("Desktop"));
        });
    }
}
