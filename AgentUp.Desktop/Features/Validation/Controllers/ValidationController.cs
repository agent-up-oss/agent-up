using AgentUp.Desktop.Features.Validation.ViewModels;
namespace AgentUp.Desktop.Features.Validation.Controllers;
public sealed class ValidationController(ValidationViewModel viewModel)
{
    public Task LoadAsync(string workspaceId, string application, CancellationToken cancellationToken = default) =>
        viewModel.LoadAsync(workspaceId, application, cancellationToken);
}
