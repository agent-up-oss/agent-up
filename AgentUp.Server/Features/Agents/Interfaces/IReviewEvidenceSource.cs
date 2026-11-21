namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>Reads a capture an agent took, such as a browser screenshot, by its artifact id.</summary>
public interface IReviewEvidenceSource
{
    Task<(string MimeType, byte[] Bytes)?> ReadAsync(string artifactId, CancellationToken cancellationToken);
}
