using AgentUp.Server.Features.Validation.DTOs;
namespace AgentUp.Server.Features.Validation.Interfaces;
public interface IValidationFlowRepository
{
    Task<IReadOnlyList<ValidationFlow>> LoadAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task SaveAsync(string workspaceId, IReadOnlyList<ValidationFlow> flows, CancellationToken cancellationToken = default);
}
