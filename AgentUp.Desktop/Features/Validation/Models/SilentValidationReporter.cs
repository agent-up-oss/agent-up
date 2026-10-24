using AgentUp.Desktop.Features.Validation.Interfaces;

namespace AgentUp.Desktop.Features.Validation.Models;

public sealed class SilentValidationReporter : IValidationReplayReporter
{
    public void BeginFlow()
    {
    }

    public void BeginStage(string stageId)
    {
    }

    public void BeginCheck(string stageId, int checkIndex)
    {
    }

    public void CompleteCheck(string stageId, int checkIndex, bool passed, string? error = null)
    {
    }

    public void CompleteStage(string stageId, bool passed, string? error = null)
    {
    }

    public void CompleteFlow(bool passed, string? message = null)
    {
    }
}
