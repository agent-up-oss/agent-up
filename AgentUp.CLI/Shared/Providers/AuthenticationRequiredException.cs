namespace AgentUp.CLI.Shared.Providers;

public sealed class AuthenticationRequiredException : Exception
{
    public const string LoginHint = "use: auth login --server";

    public AuthenticationRequiredException()
        : base(LoginHint)
    {
    }
}
