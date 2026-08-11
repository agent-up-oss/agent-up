using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.Installation.Providers;
using LocalInstaller.Core.Features.Installation.Services;
using LocalInstaller.Core.Features.PrerequisiteChecks.Models;

namespace LocalInstaller.Core.Tests.Features.Installation.Provider;

[TestFixture]
public class SampleProductWorkflowTests
{
    private static ProductManifest AcmeStudio => new("Acme Studio", "acme-studio", "ACMESTUDIO")
    {
        Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
    };

    private static ProductManifest OrbitDesk => new("Orbit Desk", "orbit-desk", "ORBITDESK")
    {
        Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
    };

    private static readonly Version V1 = new(1, 0, 0);

    private static InstallerSession AcmeSession(string installRoot = "/opt/acme-studio")
        => InstallerSession.CreateDefault(
            AcmeStudio,
            V1,
            installRoot,
            PayloadSelection.Bundled(AcmeStudio.ProductName, V1));

    [Test]
    public void SampleProduct_fullWorkflowNavigation_passesWelcomeThroughCompletion_withOnlyAcmeDerivedIdentifiers()
    {
        var session = AcmeSession();
        var snapshots = new List<InstallerSession> { session };

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.License));
        snapshots.Add(session);

        session = InstallerWorkflow.AcceptLicense(session, true);
        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Prerequisites));
        snapshots.Add(session);

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Docker));
        session = InstallerWorkflow.WithDockerStatus(session,
            new DockerStatus(DockerStatusKind.Operational, "OK", "Docker is operational.", new Version(27, 0, 0)));
        snapshots.Add(session);

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Components));
        snapshots.Add(session);

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Location));
        snapshots.Add(session);

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.ServerConfiguration));
        snapshots.Add(session);

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Payload));
        snapshots.Add(session);

        session = InstallerWorkflow.GoNext(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Summary));
        snapshots.Add(session);

        session = InstallerWorkflow.StartInstall(session);
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Progress));
        Assert.That(InstallerWorkflow.CanGoBack(session), Is.False);
        Assert.That(InstallerWorkflow.CanGoNext(session), Is.False);
        snapshots.Add(session);

        session = InstallerWorkflow.Complete(session, new ValidationReport([]));
        Assert.That(session.Step, Is.EqualTo(InstallerStep.Completion));
        snapshots.Add(session);

        foreach (var s in snapshots)
        {
            var text = string.Join(" ",
                s.ProductName,
                s.Manifest.ServiceName,
                s.Manifest.CliCommandName,
                s.Location.RootDirectory,
                s.Payload.Description);

            Assert.That(text, Does.Not.Contain("Agent-Up"),
                $"Step {s.Step}: session state contains 'Agent-Up'");
            Assert.That(text, Does.Not.Contain("agent-up"),
                $"Step {s.Step}: session state contains 'agent-up'");
            Assert.That(text, Does.Contain("Acme").Or.Contain("acme"),
                $"Step {s.Step}: session state must reference Acme Studio");
        }
    }

    [Test]
    public async Task SampleProduct_eachComponent_supportsInstallUpdateRepairUninstall_withoutModifyingAgentUpPaths()
    {
        var session = AcmeSession();
        var adapter = new FakeInstallerPlatformAdapter();
        var orbitManifest = OrbitDesk;

        foreach (var component in session.Manifest.Components)
        {
            var planTitles = new List<string>();
            var progressMessages = new List<string>();

            foreach (var action in new[]
                     {
                         InstallerComponentAction.Install,
                         InstallerComponentAction.Update,
                         InstallerComponentAction.Repair,
                         InstallerComponentAction.Uninstall
                     })
            {
                var plan = adapter.PlanComponentAction(component, action, session);
                planTitles.AddRange(plan.Select(op => op.Title));

                var progress = new List<InstallProgress>();
                await foreach (var p in adapter.ExecuteComponentActionAsync(component, action, session))
                {
                    progressMessages.Add(p.Message);
                    progress.Add(p);
                }

                Assert.That(progress, Has.Count.EqualTo(plan.Count),
                    $"{component.DisplayName} {action}: progress count must equal plan count");
            }

            var allText = string.Join(" ", planTitles.Concat(progressMessages));
            Assert.Multiple(() =>
            {
                Assert.That(allText, Does.Not.Contain("Agent-Up"),
                    $"{component.DisplayName}: plan and progress must not reference 'Agent-Up'");
                Assert.That(allText, Does.Not.Contain("agent-up"),
                    $"{component.DisplayName}: plan and progress must not reference 'agent-up'");
            });
        }

        Assert.Multiple(() =>
        {
            Assert.That(session.Location.RootDirectory, Does.Not.Contain(orbitManifest.Slug),
                "Acme Studio install root must not contain the comparison product slug");
            Assert.That(session.Manifest.ServiceName, Is.Not.EqualTo(orbitManifest.ServiceName),
                "Service names must not overlap");
            Assert.That(session.Manifest.CliCommandName, Is.Not.EqualTo(orbitManifest.CliCommandName),
                "CLI command names must not overlap");
        });
    }

    [Test]
    public void SampleProduct_serverInstall_bundlesTrayCompanion_andIncludesAutoStartInPlan()
    {
        var session = AcmeSession();

        var targetSession = InstallerComponentOperations.ForTarget(session, InstallerComponentTarget.Server);

        Assert.Multiple(() =>
        {
            Assert.That(targetSession.Components.HasFlag(InstallerComponent.Server), Is.True,
                "Server-target session must include Server");
            Assert.That(targetSession.Components.HasFlag(InstallerComponent.Tray), Is.True,
                "Server-target session must bundle Tray companion");
        });

        // Supply every possible operation kind so that IsRelevant filtering can select RegisterAutoStart
        var allOperations = Enum.GetValues<InstallOperationKind>()
            .Select(k => new InstallOperation(k, k.ToString(), false))
            .ToList<InstallOperation>();

        var serverPlan = InstallerComponentOperations.Plan(
            InstallerComponentTarget.Server,
            InstallerComponentAction.Install,
            session,
            _ => allOperations);

        Assert.That(serverPlan.Any(op => op.Kind == InstallOperationKind.RegisterAutoStart), Is.True,
            "Server install plan must include Tray autostart registration");
    }

    [Test]
    public void SampleProduct_serverUninstall_includesTrayInScope_andIncompleteRemovalRendersServerNotInstalled()
    {
        var session = AcmeSession();

        // ForTarget(Server) on an uninstall action must still scope to Server+Tray so both are removed
        var targetSession = InstallerComponentOperations.ForTarget(session, InstallerComponentTarget.Server);
        Assert.That(targetSession.Components.HasFlag(InstallerComponent.Tray), Is.True,
            "Server-target session must include Tray so its removal is in scope");

        // If the Tray autostart entry survives uninstall, Server reports as NotInstalled —
        // proving Tray removal is an integral part of Server uninstall validation
        var residualAutoStartReport = new ValidationReport(
        [
            new ValidationFinding("tray.autostart", "Tray auto-start entry was not removed.", ValidationSeverity.Error)
        ]);

        var status = InstallerComponentOperations.StatusFromValidation(
            ProductComponent.Server,
            residualAutoStartReport,
            V1);

        Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.NotInstalled),
            "Server must report NotInstalled when Tray autostart entry was not removed on uninstall");
    }

    [Test]
    public async Task SampleProduct_postInstallValidation_succeedsWithAcmeService_andDoesNotCheckAgentUpService()
    {
        var session = AcmeSession();
        var adapter = new FakeInstallerPlatformAdapter();

        var report = await adapter.ValidateInstalledStateAsync(session);

        Assert.Multiple(() =>
        {
            Assert.That(report.Succeeded, Is.True,
                "Validation must succeed when Acme service is running and Acme CLI is reachable");
            Assert.That(report.Findings.All(f =>
                    !f.Code.Contains("agent-up", StringComparison.OrdinalIgnoreCase) &&
                    !f.Message.Contains("Agent-Up", StringComparison.Ordinal)),
                Is.True,
                "No validation finding may reference Agent-Up identifiers");
        });

        Assert.Multiple(() =>
        {
            Assert.That(session.Manifest.ServiceName, Is.EqualTo("acme-studio-server"),
                "Session must use the Acme-derived service name");
            Assert.That(session.Manifest.CliCommandName, Is.EqualTo("acme-studio"),
                "Session must use the Acme-derived CLI command name");
        });
    }

    [Test]
    public async Task OtherProductAndSampleProduct_concurrentInstallSessions_completeWithOwnIdentifiers_andNeitherCorruptsTheOther()
    {
        var orbitSession = InstallerSession.CreateDefault(
            OrbitDesk,
            new Version(2, 0, 0),
            "/opt/orbit-desk",
            PayloadSelection.Bundled(OrbitDesk.ProductName, new Version(2, 0, 0)));

        var acmeSession = AcmeSession();

        var orbitAdapter = new FakeInstallerPlatformAdapter("Orbit dry run");
        var acmeAdapter = new FakeInstallerPlatformAdapter("Acme dry run");

        var orbitProgress = new List<InstallProgress>();
        var acmeProgress = new List<InstallProgress>();

        await Task.WhenAll(
            CollectInstallProgressAsync(orbitAdapter, orbitSession, orbitProgress),
            CollectInstallProgressAsync(acmeAdapter, acmeSession, acmeProgress));

        var orbitText = string.Join(" ", orbitProgress.Select(p => p.Message));
        var acmeText = string.Join(" ", acmeProgress.Select(p => p.Message));

        Assert.Multiple(() =>
        {
            Assert.That(acmeText, Does.Not.Contain("Agent-Up"),
                "Acme session progress must not reference 'Agent-Up'");
            Assert.That(acmeText, Does.Not.Contain("agent-up"),
                "Acme session progress must not reference 'agent-up'");
            Assert.That(orbitText, Does.Not.Contain("Acme"),
                "Orbit Desk session progress must not reference 'Acme'");
            Assert.That(acmeText, Does.Contain("acme-studio").Or.Contain("Acme Studio"),
                "Acme session progress must reference Acme Studio identifiers");
            Assert.That(orbitText, Does.Contain("orbit-desk").Or.Contain("Orbit Desk"),
                "Orbit Desk session progress must reference Orbit Desk identifiers");
        });

        var orbitReport = await orbitAdapter.ValidateInstalledStateAsync(orbitSession);
        var acmeReport = await acmeAdapter.ValidateInstalledStateAsync(acmeSession);

        Assert.That(orbitReport.Succeeded, Is.True, "Orbit Desk concurrent install must validate successfully");
        Assert.That(acmeReport.Succeeded, Is.True, "Acme Studio concurrent install must validate successfully");
    }

    [Test]
    public async Task SampleProduct_fullInstallWorkflow_everyPlanItemProgressEventAndCompletionReport_containsOnlyAcmeStrings()
    {
        var session = AcmeSession();
        var adapter = new FakeInstallerPlatformAdapter();

        var plan = adapter.PlanInstall(session);

        var progressEvents = new List<InstallProgress>();
        await foreach (var p in adapter.ExecuteInstallAsync(session))
            progressEvents.Add(p);

        var report = await adapter.ValidateInstalledStateAsync(session);

        var allPlanText = string.Join(" ", plan.Select(op => op.Title));
        var allProgressText = string.Join(" ", progressEvents.Select(p => p.Message));
        var allFindingText = string.Join(" ", report.Findings.Select(f => $"{f.Code} {f.Message}"));

        var allOutput = string.Join(" ", allPlanText, allProgressText, allFindingText);

        Assert.Multiple(() =>
        {
            Assert.That(allOutput, Does.Not.Contain("Agent-Up"),
                "No plan item, progress event, or completion report may reference 'Agent-Up'");
            Assert.That(allOutput, Does.Not.Contain("agent-up"),
                "No plan item, progress event, or completion report may reference 'agent-up'");
        });

        // Progress count must match plan — no events are silently dropped
        Assert.That(progressEvents, Has.Count.EqualTo(plan.Count),
            "Every planned operation must produce a progress event");

        // The install plan must reference Acme Studio's own service name
        Assert.That(plan.Any(op => op.Title.Contains("acme-studio-server", StringComparison.Ordinal)), Is.True,
            "Install plan must reference the Acme Studio service name, not a generic placeholder");
    }

    private static async Task CollectInstallProgressAsync(
        FakeInstallerPlatformAdapter adapter,
        InstallerSession session,
        List<InstallProgress> collected)
    {
        await foreach (var p in adapter.ExecuteInstallAsync(session))
            collected.Add(p);
    }
}
