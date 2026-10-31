namespace AgentUp.Desktop.Features.Interface.Interfaces;

/// <summary>Where an export of the decision backlog ends up once the Server has written it out.</summary>
public interface IDecisionExportSink
{
    /// <summary>Saves the export, answering false where the reader changed their mind.</summary>
    Task<bool> SaveAsync(string suggestedFileName, string jsonl, CancellationToken cancellationToken = default);
}
