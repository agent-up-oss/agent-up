namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// Raised when a repository declares a verification section that cannot be trusted.
/// Verification fails closed on purpose: a gate that silently disables itself on a typo
/// is worse than no gate, because it reports success.
/// </summary>
public sealed class VerificationConfigurationException : Exception
{
    public VerificationConfigurationException(string message)
        : base(message)
    {
    }

    public VerificationConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
