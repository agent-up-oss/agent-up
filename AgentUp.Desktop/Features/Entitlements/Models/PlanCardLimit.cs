namespace AgentUp.Desktop.Features.Entitlements.Models;

public sealed record PlanCardLimit(string Id, long? Max, long? Used)
{
    public string Label => Max is null && Used is null
        ? $"{Id}: none"
        : Max is null
            ? $"{Id}: {Used} used"
            : Used is null
                ? $"{Id}: max {Max}"
                : $"{Id}: {Used} of {Max}";
}
