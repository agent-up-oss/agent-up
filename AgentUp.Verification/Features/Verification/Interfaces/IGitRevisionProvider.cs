namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>Resolves the current HEAD revision for an export, or a fallback when Git is absent.</summary>
public interface IGitRevisionProvider
{
    string Resolve(string repositoryRoot);
}
