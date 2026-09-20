using System.Security.Claims;

namespace AgentUp.Server.Features.Authentication.Providers;

public static class BoundWorkspaceFilter
{
    public static IReadOnlyList<T> Visible<T>(ClaimsPrincipal user, IReadOnlyList<T> items, Func<T, string> id)
    {
        var boundWorkspace = user.FindFirst("workspace")?.Value;
        if (string.IsNullOrWhiteSpace(boundWorkspace))
            return items;

        return items
            .Where(item => string.Equals(id(item), boundWorkspace, StringComparison.Ordinal))
            .ToList();
    }
}
