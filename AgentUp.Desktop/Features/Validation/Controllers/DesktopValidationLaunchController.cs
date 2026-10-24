using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Services;

namespace AgentUp.Desktop.Features.Validation.Controllers;

public sealed class DesktopValidationLaunchController(
    IDesktopValidationReplayHost host,
    DesktopValidationLaunchService service)
{
    public bool IsRequested => service.IsRequested;

    /// <summary>
    /// Interactive Desktop, including Product screens, starts with the picker hidden.
    /// Wait for a human sign-in unless this process was launched for unattended validate.
    /// </summary>
    public bool ShouldPromptInteractively => !IsRequested;

    public string? AdminPassword => service.AdminPassword;

    public void Attach(object window, object viewModel) => host.Attach(window, viewModel);

    public Task<int?> RunIfRequestedAsync(
        object window,
        object viewModel,
        CancellationToken cancellationToken)
        => service.RunIfRequestedAsync(window, viewModel, cancellationToken);
}
