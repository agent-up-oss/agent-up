namespace AgentUp.Desktop.Features.Workspaces.Interfaces;

public interface IReviewReportSavePrompt
{
    Task<bool> SaveAsync(string suggestedFileName, string html, CancellationToken cancellationToken = default);
}
