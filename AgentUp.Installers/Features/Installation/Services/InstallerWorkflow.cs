using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Features.Installation.Services;

public static class InstallerWorkflow
{
    private static readonly InstallerStep[] OrderedSteps =
    [
        InstallerStep.Welcome,
        InstallerStep.License,
        InstallerStep.Prerequisites,
        InstallerStep.Docker,
        InstallerStep.Components,
        InstallerStep.Location,
        InstallerStep.ServerConfiguration,
        InstallerStep.Payload,
        InstallerStep.Summary,
        InstallerStep.Progress,
        InstallerStep.Completion
    ];

    public static bool CanGoBack(InstallerSession session)
        => session.Step is not (InstallerStep.Welcome or InstallerStep.Progress);

    public static bool CanGoNext(InstallerSession session)
        => session.Step switch
        {
            InstallerStep.License => session.LicenseAccepted,
            InstallerStep.Docker => session.DockerStatus?.CanContinue == true,
            InstallerStep.Progress => false,
            InstallerStep.Completion => false,
            _ => true
        };

    public static InstallerSession GoNext(InstallerSession session)
        => CanGoNext(session)
            ? session with { Step = NextStep(session.Step) }
            : session;

    public static InstallerSession GoBack(InstallerSession session)
        => CanGoBack(session)
            ? session with { Step = PreviousStep(session.Step) }
            : session;

    public static InstallerSession AcceptLicense(InstallerSession session, bool accepted)
        => session with { LicenseAccepted = accepted };

    public static InstallerSession WithDockerStatus(InstallerSession session, DockerStatus status)
        => session with { DockerStatus = status };

    public static InstallerSession StartInstall(InstallerSession session)
        => session with { Step = InstallerStep.Progress };

    public static InstallerSession Complete(InstallerSession session, ValidationReport report)
        => session with { Step = InstallerStep.Completion, ValidationReport = report };

    private static InstallerStep NextStep(InstallerStep step)
    {
        var index = Array.IndexOf(OrderedSteps, step);
        return OrderedSteps[Math.Min(index + 1, OrderedSteps.Length - 1)];
    }

    private static InstallerStep PreviousStep(InstallerStep step)
    {
        var index = Array.IndexOf(OrderedSteps, step);
        return OrderedSteps[Math.Max(index - 1, 0)];
    }
}
