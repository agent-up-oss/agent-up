using AgentUp.Server.Features.Validation.DTOs;
namespace AgentUp.Server.Features.Validation.Interfaces;
public interface IValidationFlowRepository
{
    Task<IReadOnlyList<ValidationFlow>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<ValidationFlow> flows, CancellationToken cancellationToken = default);
}
