using System.Xml.Linq;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Coverage.Providers;

/// <summary>
/// Parses one Cobertura XML document into repo-relative line coverage.
/// </summary>
/// <remarks>
/// Pure, so the two fiddly parts are testable without a coverage run. First, coverlet
/// writes each class's filename relative to a &lt;sources&gt; root, and on Linux that root
/// is "/" with the filename carrying the rest of the absolute path — so a repo-relative
/// path only falls out after joining each source and relativizing against the repository
/// root. Second, a class's own &lt;lines&gt; block is a complete superset of its methods'
/// blocks, so reading the class level avoids double-counting.
/// </remarks>
public sealed class CoberturaReportParser
{
    public IReadOnlyList<FileCoverage> Parse(string xml, string repositoryRoot)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new CoverageConfigurationException(
                $"A coverage report is not valid XML: {exception.Message}", exception);
        }

        var root = document.Root;
        if (root is null)
            return [];

        var sources = root.Elements("sources").Elements("source")
            .Select(source => source.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return
        [
            .. root.Descendants("class")
                .Select(element => (
                    Path: ResolvePath(element.Attribute("filename")?.Value, sources, repositoryRoot),
                    Lines: ReadLines(element)))
                .Where(item => item.Path is not null && item.Lines.Count > 0)
                .Select(item => new FileCoverage(item.Path!, item.Lines))
        ];
    }

    private static IReadOnlyDictionary<int, int> ReadLines(XElement classElement)
        => classElement.Elements("lines").Elements("line")
            .Select(line => (
                Number: ParseInt(line.Attribute("number")?.Value),
                Hits: ParseInt(line.Attribute("hits")?.Value)))
            .Where(line => line.Number is not null && line.Hits is not null)
            .GroupBy(line => line.Number!.Value)
            .ToDictionary(group => group.Key, group => group.Max(line => line.Hits!.Value));

    /// <summary>
    /// Turns a report filename into a repo-relative path, or null when the file lies
    /// outside this repository (a referenced package's sources, for instance).
    /// </summary>
    private static string? ResolvePath(string? filename, IReadOnlyList<string> sources, string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        var fullRoot = Path.GetFullPath(repositoryRoot);

        // Every candidate is made absolute against an explicit base. Leaving a relative
        // candidate to Path.GetFullPath would resolve it against the current working
        // directory, which silently admits paths from outside the repository whenever the
        // process happens to be running inside it.
        // With <sources> present, the filename is relative to one of them and nothing else.
        // Falling back to "treat it as repo-relative" would admit any out-of-repository
        // path, such as a referenced package's own sources.
        var candidates = sources.Count > 0
            ? sources.Select(source => Path.IsPathRooted(source)
                ? Path.Join(source, filename)
                : Path.Join(fullRoot, source, filename))
            : [Path.IsPathRooted(filename) ? filename : Path.Join(fullRoot, filename)];

        return candidates
            .Select(candidate => Relativize(candidate, fullRoot))
            .FirstOrDefault(relative => relative is not null);
    }

    private static string? Relativize(string candidate, string fullRoot)
    {
        var full = Path.GetFullPath(candidate);
        if (!full.StartsWith(fullRoot, StringComparison.Ordinal))
            return null;

        var relative = Path.GetRelativePath(fullRoot, full);
        return relative.StartsWith("..", StringComparison.Ordinal)
            ? null
            : PathGlobProvider.Normalize(relative);
    }

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;
}
