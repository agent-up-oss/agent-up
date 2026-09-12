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
