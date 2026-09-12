namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// Raised when the coverage section cannot be trusted. Fails closed for the same reason
/// the verification loader does: a gate that disables itself on a typo reports success.
/// </summary>
public sealed class CoverageConfigurationException : Exception
{
    public CoverageConfigurationException(string message)
        : base(message)
    {
    }

    public CoverageConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
