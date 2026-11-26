namespace AgentUp.Server.Features.TaskSource.Models;

/// <summary>
/// The two letters a round member banner shows when there is no portrait. Two words become the
/// first letter of each; a single word takes its first two characters.
/// </summary>
public static class MemberInitials
{
    public static string From(string name)
    {
        var parts = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}
