using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

internal static class DiagnosticEventFilter
{
    internal static bool MatchesCategories(
        ApplicationAuditEventDto dto,
        IEnumerable<DiagnosticKindFilterOption> filters)
    {
        var selected = filters.Where(filter => filter.IsSelected).ToList();
        if (selected.Count == 0)
            return false;

        return selected.Any(filter => MatchesFilter(dto, filter));
    }

    internal static bool MatchesSearch(ApplicationAuditEventDto dto, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        var needle = searchText.Trim();
        if (Contains(dto.Action, needle)
            || Contains(dto.Kind, needle)
            || Contains(dto.Outcome, needle))
            return true;

        return dto.Details.Any(pair => Contains(pair.Key, needle) || Contains(pair.Value, needle));
    }

    private static bool MatchesFilter(ApplicationAuditEventDto dto, DiagnosticKindFilterOption filter)
    {
        if (!string.Equals(dto.Kind, filter.Kind, StringComparison.OrdinalIgnoreCase))
            return false;

        if (filter.Stream is null)
            return true;

        return dto.Details.TryGetValue("stream", out var stream)
               && string.Equals(stream, filter.Stream, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string needle)
        => !string.IsNullOrEmpty(value)
           && value.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
