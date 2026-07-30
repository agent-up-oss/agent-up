using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Tests.Features.Installation.Unit;

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

    private static IEnumerable<TestCaseData> ManifestCases()
    {
        yield return new TestCaseData(ProductManifest.AgentUp()).SetName("AgentUp");
        yield return new TestCaseData(
            new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
            {
                Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
            }).SetName("SampleProduct_AcmeStudio");
    }

    [TestCaseSource(nameof(ManifestCases))]
    public void Workflow_forAnyManifest_advancesLicenseThenBlocksOnDockerThenReachesCompletion(ProductManifest manifest)
    {
        var session = SessionFor(manifest);

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.License),
            $"[{manifest.ProductName}] Welcome must advance to License");

        session = InstallerWorkflow.AcceptLicense(session, true);
        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Prerequisites),
            $"[{manifest.ProductName}] accepted License must advance to Prerequisites");

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Docker),
            $"[{manifest.ProductName}] Prerequisites must advance to Docker");

        session = InstallerWorkflow.WithDockerStatus(session,
            new DockerStatus(DockerStatusKind.Operational, "OK", "Docker is operational.", new Version(27, 0, 0)));
        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Components),
            $"[{manifest.ProductName}] operational Docker must advance to Components");

        session = InstallerWorkflow.StartInstall(InstallerWorkflow.GoNext(
            InstallerWorkflow.GoNext(
                InstallerWorkflow.GoNext(
                    InstallerWorkflow.GoNext(session)))));
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Progress),
            $"[{manifest.ProductName}] must reach Progress after Summary");
        Assert.That(InstallerWorkflow.CanGoBack(session), Is.False);
        Assert.That(InstallerWorkflow.CanGoNext(session), Is.False);
    }

    private static InstallerSession NewSession()
        => InstallerSession.CreateDefault(ProductManifest.AgentUp(), new Version(1, 2, 3), "/opt/agent-up", PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static InstallerSession SessionFor(ProductManifest manifest)
        => InstallerSession.CreateDefault(manifest, new Version(1, 2, 3), manifest.DefaultInstallRoot(),
            PayloadSelection.Bundled(manifest.ProductName, new Version(1, 2, 3)));
}
