namespace AgentUp.Desktop.Features.Audit.ViewModels;

internal static class DiagnosticKindFilterQuery
{
    internal static (IReadOnlyList<string> Kinds, IReadOnlyList<string> Streams) FromSelection(
        IEnumerable<DiagnosticKindFilterOption> filters)
    {
        var selected = filters.Where(filter => filter.IsSelected).ToList();
        var kinds = selected
            .Select(filter => filter.Kind)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var streams = selected
            .Where(filter => filter.Stream is not null)
            .Select(filter => filter.Stream!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (kinds, streams);
    }
}
