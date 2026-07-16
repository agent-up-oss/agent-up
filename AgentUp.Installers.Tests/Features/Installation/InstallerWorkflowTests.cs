using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Tests.Features.Installation;

[TestFixture]
public class InstallerWorkflowTests
{
    [Test]
    public void GoNext_requiresAcceptedLicenseOnLicenseStep()
    {
        var session = NewSession() with { Step = InstallerStep.License };

        Assert.That(InstallerWorkflow.CanGoNext(session), Is.False);
        Assert.That(InstallerWorkflow.GoNext(session).Step, Is.EqualTo(InstallerStep.License));

        var accepted = InstallerWorkflow.AcceptLicense(session, true);
        Assert.That(InstallerWorkflow.CanGoNext(accepted), Is.True);
        Assert.That(InstallerWorkflow.GoNext(accepted).Step, Is.EqualTo(InstallerStep.Prerequisites));
    }

    [Test]
    public void GoNext_requiresOperationalDockerOnDockerStep()
    {
        var session = NewSession() with
        {
            Step = InstallerStep.Docker,
            DockerStatus = new DockerStatus(DockerStatusKind.DaemonNotRunning, "Stopped", "Daemon is stopped.")
        };

        Assert.That(InstallerWorkflow.CanGoNext(session), Is.False);

        var operational = InstallerWorkflow.WithDockerStatus(session,
            new DockerStatus(DockerStatusKind.Operational, "Operational", "Docker works."));

        Assert.That(InstallerWorkflow.CanGoNext(operational), Is.True);
        Assert.That(InstallerWorkflow.GoNext(operational).Step, Is.EqualTo(InstallerStep.Components));
    }

    [Test]
    public void StartAndCompleteInstallationMoveThroughProgressAndCompletion()
    {
        var session = NewSession() with { Step = InstallerStep.Summary };

        var progress = InstallerWorkflow.StartInstall(session);

        Assert.That(progress.Step, Is.EqualTo(InstallerStep.Progress));
        Assert.That(InstallerWorkflow.CanGoBack(progress), Is.False);
        Assert.That(InstallerWorkflow.CanGoNext(progress), Is.False);
    }

    private static InstallerSession NewSession()
        => InstallerSession.CreateDefault("Agent-Up", new Version(1, 2, 3), "/opt/agent-up", PayloadSelection.Bundled(new Version(1, 2, 3)));
}
