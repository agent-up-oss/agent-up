namespace AgentUp.Server.Features.Training.Models;

/// <summary>
/// A picture that shows what a decision was based on, kept by the backlog itself.
///
/// The bytes are copied in when the decision is recorded rather than pointed at where the agent
/// found them. A decision is training material and outlives the run that took it: the workspace
/// gets cleaned, the branch moves on, and an audit event has its own reasons to be rolled away.
/// None of that may take the evidence with it.
/// </summary>
/// <param name="EvidenceId">This backlog's own id for the image, which is what a surface asks for.</param>
/// <param name="SourceArtifactId">
/// The audit artifact it was copied from, kept so a reader can still trace it back to the capture
/// that produced it.
/// </param>
public sealed record DecisionEvidenceImage(string EvidenceId, string SourceArtifactId, string MimeType);
