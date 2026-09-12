using System.Text.Json;
using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Rules over the "coverage" section of agent-up.json.
/// </summary>
/// <remarks>
/// The reachability rule is the coverage-side counterpart of
/// <see cref="VerificationCoverage"/>: a production project absent from the include list is
/// silently unmeasured, which is the same class of blind spot that let
/// AgentUp.Browser.Streaming go untested.
/// </remarks>
[TestFixture]
public sealed class CoverageConfiguration
{
    [Test]
    public void Every_production_project_is_measured_by_a_coverage_include_glob()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var include = ReadGlobs(root, "include");

        var unmeasured = ArchitectureFixture.ProductionProjects
            .Where(project => !include.Any(glob => glob.StartsWith(project + "/", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .Select(project => $"{project} is not covered by any 'coverage.include' glob, so it is never measured")
            .ToArray();

        Assert.That(unmeasured, Is.Empty,
            "Add every production project to 'coverage.include' in agent-up.json.");
    }

    [Test]
    public void Coverage_minimum_is_declared_and_at_least_ninety()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var document = ReadAgentUpJson(root);
        var section = document.RootElement.GetProperty("coverage");

        Assert.That(section.GetProperty("minimum").GetDouble(), Is.GreaterThanOrEqualTo(90d),
            "Patch coverage must stay at or above the agreed 90% floor.");
    }

    [Test]
    public void Slice_coverage_floor_is_declared()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var document = ReadAgentUpJson(root);
        var section = document.RootElement.GetProperty("coverage");

        // The setting is optional in the loader, because a repository without a slice
        // layout has no slices to hold to a floor. This one has them, so leaving it out
        // would switch the floor off while the configuration still looked populated.
        Assert.That(section.TryGetProperty("sliceMinimum", out var minimum), Is.True,
            "'coverage.sliceMinimum' must be declared, or per-slice coverage measures nothing.");
        Assert.That(minimum.GetDouble(), Is.GreaterThan(0d));
    }

    [Test]
    public void Every_slice_coverage_exemption_names_a_slice_that_exists()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var exemptions = ReadGlobs(root, "sliceExemptions");
        TestContext.Out.WriteLine($"Slice coverage exemptions: {exemptions.Count} entry(ies).");

        // A renamed or deleted slice leaves an entry that can never match, so the list
        // reads as accepted debt while exempting nothing.
        var missing = exemptions
            .Where(slice => !Directory.Exists(Path.Join(root, slice)))
            .Order(StringComparer.Ordinal)
            .Select(slice => $"'{slice}' is exempt from the slice floor but no such directory exists")
            .ToArray();

        Assert.That(missing, Is.Empty,
            "Delete the entry, or correct it to the slice's current path.");
    }

    [Test]
    public void Codecov_ignores_everything_the_local_gate_excludes()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var ignored = ReadCodecovIgnores(root);

        // Codecov measures the same change from the same reports but applies its own ignore
        // list. A glob missing here fails a pull request the local gate passed, on lines
        // this repository has already decided carry no information - which is how a gate
        // stops being believed. Extra entries are fine: agent-up.json narrows itself with
        // 'coverage.include' instead, so codecov.yml has to exclude test projects by hand.
        var missing = ReadGlobs(root, "exclude")
            .Where(glob => !ignored.Contains(glob))
            .Order(StringComparer.Ordinal)
            .Select(glob => $"codecov.yml does not ignore '{glob}'")
            .ToArray();

        Assert.That(missing, Is.Empty,
            "Add the glob to the 'ignore' list in codecov.yml so both views of coverage agree.");
    }

    /// <summary>
    /// The quoted entries of codecov.yml's top-level "ignore" list. Read by hand rather
    /// than with a YAML parser, which the suite would otherwise need a dependency for.
    /// </summary>
    private static HashSet<string> ReadCodecovIgnores(string root)
    {
        var path = Path.Join(root, "codecov.yml");
        if (!File.Exists(path))
            throw new FileNotFoundException("codecov.yml is missing.", path);

        return File.ReadAllLines(path)
            .SkipWhile(line => line.TrimEnd() != "ignore:")
            .Skip(1)
            .TakeWhile(line => line.StartsWith(' ') || line.Length == 0)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim().Trim('"', '\''))
            .ToHashSet(StringComparer.Ordinal);
    }

    [Test]
    public void Coverage_exclusions_stay_reviewable_rather_than_open_ended()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var exclude = ReadGlobs(root, "exclude");

        // A bare "**" or a whole-project exclusion would silently switch the gate off for
        // that code while leaving the configuration looking populated.
        var tooBroad = exclude
            .Where(glob => glob is "**" or "**/*" or "**/*.cs")
            .Select(glob => $"'{glob}' excludes everything from the coverage gate")
            .ToArray();

        Assert.That(tooBroad, Is.Empty, "Keep coverage exclusions specific enough to review in a diff.");
    }

    private static JsonDocument ReadAgentUpJson(string root)
        => JsonDocument.Parse(File.ReadAllText(Path.Join(root, "agent-up.json")));

    private static IReadOnlyList<string> ReadGlobs(string root, string name)
    {
        using var document = ReadAgentUpJson(root);
        return
        [
            .. document.RootElement.GetProperty("coverage").GetProperty(name).EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
        ];
    }
}
