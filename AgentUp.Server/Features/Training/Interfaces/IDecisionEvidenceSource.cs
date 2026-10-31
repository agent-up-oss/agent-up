namespace AgentUp.Server.Features.Training.Interfaces;

/// <summary>
/// Where an agent's evidence picture is read from when a decision is recorded: the capture it
/// already made, named by the artifact id the browser tool handed back.
/// </summary>
public interface IDecisionEvidenceSource
{
    Task<(string MimeType, byte[] Bytes)?> ReadAsync(string artifactId, CancellationToken cancellationToken);
}
