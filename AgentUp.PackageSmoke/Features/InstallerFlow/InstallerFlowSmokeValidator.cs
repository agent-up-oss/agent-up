using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Flow;
using AgentUp.Installers.Features.Payloads;
using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.InstallerFlow;

public sealed class InstallerFlowSmokeValidator
{
    public async Task<PackageValidationResult> ValidateAsync(
        string platform,
        string workDirectory,
        CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        Directory.CreateDirectory(workDirectory);

        var version = new Version(0, 0, 0);
        var session = InstallerSession.CreateDefault(
            "Agent-Up",
            version,
            DefaultInstallRoot(platform),
            PayloadSelection.Bundled(version));
        var adapter = new FakeInstallerPlatformAdapter($"{platform} dry run");

        session = InstallerWorkflow.GoNext(session);
        session = InstallerWorkflow.AcceptLicense(session, true);
        session = InstallerWorkflow.GoNext(session);
        session = InstallerWorkflow.WithDockerStatus(session, await adapter.CheckDockerAsync(cancellationToken));
        session = InstallerWorkflow.GoNext(session);
        session = InstallerWorkflow.GoNext(session);
        session = InstallerWorkflow.GoNext(session);
        session = InstallerWorkflow.GoNext(session);
        session = InstallerWorkflow.GoNext(session);
        session = InstallerWorkflow.GoNext(session);

        if (session.Step != InstallerStep.Summary)
            assert.Error("installer.flow.summary", $"Expected Summary step, got {session.Step}.");

        var plan = adapter.PlanInstall(session);
        if (plan.Count == 0)
            assert.Error("installer.flow.plan", "Installer plan is empty.");
        if (!plan.Any(operation => operation.RequiresElevation))
            assert.Error("installer.flow.elevation", "Installer plan did not mark any native operation as elevation-required.");

        session = InstallerWorkflow.StartInstall(session);
        var progress = new List<InstallProgress>();
        await foreach (var item in adapter.ExecuteInstallAsync(session, cancellationToken))
            progress.Add(item);
        if (progress.Count != plan.Count)
            assert.Error("installer.flow.progress", $"Expected {plan.Count} progress events, got {progress.Count}.");

        var report = await adapter.ValidateInstalledStateAsync(session, cancellationToken);
        session = InstallerWorkflow.Complete(session, report);
        if (session.Step != InstallerStep.Completion)
            assert.Error("installer.flow.completion", $"Expected Completion step, got {session.Step}.");
        if (!report.Succeeded)
            assert.Error("installer.flow.validation", "Dry-run installed-state validation failed.");

        await File.WriteAllLinesAsync(
            Path.Combine(workDirectory, "installer-flow.log"),
            progress.Select(item => $"{item.CompletedOperations}/{item.TotalOperations}: {item.Message}"),
            cancellationToken);

        return new PackageValidationResult(null, null, assert.Findings);
    }

    private static string DefaultInstallRoot(string platform)
        => platform switch
        {
            "windows" => @"C:\Program Files\Agent-Up",
            "macos" => "/Applications/Agent-Up.app",
            _ => "/opt/agent-up"
        };
}
