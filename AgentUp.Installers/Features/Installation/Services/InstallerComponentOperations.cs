using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.Installation.Services;

public static class InstallerComponentOperations
{
    public static InstallerSession ForTarget(InstallerSession session, InstallerComponentTarget target) =>
        session with
        {
            Components = InstallerComponent.RuntimeDependencies | (target switch
            {
                InstallerComponentTarget.Desktop => InstallerComponent.Desktop,
                InstallerComponentTarget.Server => InstallerComponent.Server | InstallerComponent.NativeService,
                InstallerComponentTarget.Cli => InstallerComponent.Cli,
                _ => InstallerComponent.None
            })
        };

    public static IReadOnlyList<InstallOperation> Plan(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session,
        Func<InstallerSession, IReadOnlyList<InstallOperation>> fullPlan)
    {
        if (action == InstallerComponentAction.Uninstall)
        {
            return
            [
                new(TargetOperationKind(target), $"Uninstall {DisplayName(target)}", true),
                new(InstallOperationKind.ValidateInstallation, $"Validate {DisplayName(target)} removal", false)
            ];
        }

        var targetSession = ForTarget(session, target);
        return fullPlan(targetSession)
            .Where(operation => IsRelevant(operation.Kind, target))
            .ToList();
    }

    public static async IAsyncEnumerable<InstallProgress> ExecuteInstallLikeAction(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session,
        Func<InstallerSession, CancellationToken, IAsyncEnumerable<InstallProgress>> executeInstall,
        Func<InstallerComponentTarget, InstallerSession, CancellationToken, IAsyncEnumerable<InstallProgress>> executeUninstall,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (action == InstallerComponentAction.Uninstall)
        {
            await foreach (var item in executeUninstall(target, session, cancellationToken)
                               .WithCancellation(cancellationToken))
                yield return item;
            yield break;
        }

        await foreach (var item in executeInstall(ForTarget(session, target), cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            var isRelevant = IsRelevant(item.Kind, target);
            if (isRelevant)
                yield return item;
        }
    }

    public static InstallerComponentStatus StatusFromValidation(
        ProductComponent component,
        ValidationReport report,
        Version expectedVersion)
    {
        var codePrefixes = CodePrefixesFor(component);

        var errors = report.Findings
            .Where(finding =>
                finding.Severity == ValidationSeverity.Error
                && codePrefixes.Any(prefix => finding.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (errors.Any(finding => finding.Code.EndsWith(".missing", StringComparison.OrdinalIgnoreCase)
                                  || finding.Code.EndsWith(".path", StringComparison.OrdinalIgnoreCase)))
        {
            return new InstallerComponentStatus(component, InstallerComponentStatusKind.NotInstalled);
        }

        if (errors.Any(finding => finding.Code.EndsWith(".version", StringComparison.OrdinalIgnoreCase)))
        {
            return new InstallerComponentStatus(component, InstallerComponentStatusKind.UpdateAvailable, AvailableVersion: expectedVersion);
        }

        return new InstallerComponentStatus(component, InstallerComponentStatusKind.Installed, expectedVersion, expectedVersion);
    }

    private static IReadOnlyList<string> CodePrefixesFor(ProductComponent component)
    {
        if (!Enum.TryParse<InstallerComponentTarget>(component.Id, true, out var target))
            return [];

        return target switch
        {
            InstallerComponentTarget.Desktop => ["desktop."],
            InstallerComponentTarget.Server => ["service.", "server."],
            InstallerComponentTarget.Cli => ["cli."],
            _ => []
        };
    }

    private static bool IsRelevant(InstallOperationKind kind, InstallerComponentTarget target) =>
        kind is InstallOperationKind.ValidatePrerequisites or InstallOperationKind.StagePayload or InstallOperationKind.InstallFiles or InstallOperationKind.ValidateInstallation
        || kind == TargetOperationKind(target)
        || target == InstallerComponentTarget.Desktop && kind == InstallOperationKind.RegisterUninstall;

    public static InstallOperationKind TargetOperationKind(InstallerComponentTarget target) =>
        target switch
        {
            InstallerComponentTarget.Server => InstallOperationKind.RegisterService,
            InstallerComponentTarget.Cli => InstallOperationKind.RegisterCli,
            InstallerComponentTarget.Desktop => InstallOperationKind.RegisterDesktop,
            _ => InstallOperationKind.InstallFiles
        };

    private static string DisplayName(InstallerComponentTarget target) =>
        target == InstallerComponentTarget.Cli ? "CLI" : $"{target}";
}
