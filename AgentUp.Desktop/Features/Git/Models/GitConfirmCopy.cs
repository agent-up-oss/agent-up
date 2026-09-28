namespace AgentUp.Desktop.Features.Git.Models;

public enum GitConfirmKind
{
    None,
    Push,
    ForcePush,
    Discard
}

public sealed record GitConfirmCopy(
    string Title,
    string Message,
    string Confirm,
    bool Destructive = false);
