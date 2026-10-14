namespace AgentUp.Sdk.Forge;

public sealed class ForgeProtectedRefException : InvalidOperationException
{
    public ForgeProtectedRefException(string @ref)
        : base($"Refusing to write protected ref '{@ref}'.")
    {
        Ref = @ref;
    }

    public string Ref { get; }
}
