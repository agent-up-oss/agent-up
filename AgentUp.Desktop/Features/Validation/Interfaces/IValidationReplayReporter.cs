namespace AgentUp.Desktop.Features.Validation.Interfaces;

public interface IValidationReplayReporter
{
    void BeginFlow();

    void BeginStage(string stageId);

    void BeginCheck(string stageId, int checkIndex);

    void CompleteCheck(string stageId, int checkIndex, bool passed, string? error = null);

    void CompleteStage(string stageId, bool passed, string? error = null);

    void CompleteFlow(bool passed, string? message = null);
}
