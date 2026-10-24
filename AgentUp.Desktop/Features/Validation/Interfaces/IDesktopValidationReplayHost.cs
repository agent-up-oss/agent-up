using AgentUp.Desktop.Features.Validation.DTOs;

namespace AgentUp.Desktop.Features.Validation.Interfaces;

public interface IDesktopValidationReplayHost
{
    void Attach(object window, object viewModel);

    Task<string?> PrepareAsync(ValidationFlowDto flow, CancellationToken cancellationToken);

    Task<string?> ExecuteAsync(ValidationStepDto step, CancellationToken cancellationToken);

    Task<ValidationExpectationReportDto> EvaluateAsync(
        ValidationAssertionDto assertion,
        CancellationToken cancellationToken);

    DesktopResolvedTargetDto? LastResolution { get; }

    IReadOnlyList<DesktopTargetCandidateDto> LastCandidates { get; }

    Task CapturePngAsync(string path, CancellationToken cancellationToken);
}
