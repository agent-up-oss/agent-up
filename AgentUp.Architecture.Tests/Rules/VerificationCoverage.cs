using System.Text.Json;
using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Rules over the "verification" section of agent-up.json, which decides what the runtime
/// requires when a given path changes.
/// </summary>
/// <remarks>
/// The reachability rule is the structural fix for the blind spot that let
/// AgentUp.Browser.Streaming carry 2,000 lines of production code with no test project and
/// no configuration entry: it was invisible to every other guard at once.
/// </remarks>
[TestFixture]
public sealed class VerificationCoverage
{
    [Test]
    public void Every_production_project_is_reachable_by_a_verification_path_rule()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var rules = PathRules(root);

        var unreachable = ArchitectureFixture.ProductionProjects
            .Where(project => !rules.Any(rule => MatchesProject(rule.Match, project)))
            .Order(StringComparer.Ordinal)
            .Select(project => $"{project} has no verification path rule, so changing it requires no checks")
            .ToArray();

        Assert.That(unreachable, Is.Empty,
            "Add a 'verification.paths' rule for every production project in agent-up.json.");
    }

    [Test]
    public void Every_test_project_is_reachable_by_a_verification_path_rule()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var rules = PathRules(root);

        var unreachable = ArchitectureFixture.TestProjects
            .Where(project => Directory.Exists(Path.Join(root, project)))
            .Where(project => !rules.Any(rule => MatchesProject(rule.Match, project)))
            .Order(StringComparer.Ordinal)
            .Select(project => $"{project} has no verification path rule, so a test-only change requires no checks")
            .ToArray();

        Assert.That(unreachable, Is.Empty,
            "A test-only change must still run the suite it belongs to; map every test project in agent-up.json.");
    }

    [Test]
    public void Every_referenced_verification_check_is_defined()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var document = ReadAgentUpJson(root);
        var section = document.RootElement.GetProperty("verification");
        var defined = section.GetProperty("checks").EnumerateObject().Select(check => check.Name).ToHashSet(StringComparer.Ordinal);

        var referenced = PathRules(root)
            .SelectMany(rule => rule.Checks.Select(check => (rule.Match, Check: check)))
            .Concat(section.GetProperty("always").EnumerateArray()
                .Select(always => (Match: "always", Check: always.GetString() ?? string.Empty)));

        var dangling = referenced
            .Where(reference => !defined.Contains(reference.Check))
            .Select(reference => $"'{reference.Match}' references undefined check '{reference.Check}'")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(dangling, Is.Empty, "Every check id used by a path rule must exist in 'verification.checks'.");
    }

    [Test]
    public void Every_defined_verification_check_is_referenced()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var document = ReadAgentUpJson(root);
        var section = document.RootElement.GetProperty("verification");

        var referenced = PathRules(root)
            .SelectMany(rule => rule.Checks)
            .Concat(section.GetProperty("always").EnumerateArray().Select(always => always.GetString() ?? string.Empty))
            .ToHashSet(StringComparer.Ordinal);

        var orphans = section.GetProperty("checks").EnumerateObject()
            .Select(check => check.Name)
            .Where(name => !referenced.Contains(name))
            .Order(StringComparer.Ordinal)
            .Select(name => $"check '{name}' is defined but no path rule or 'always' entry requires it")
            .ToArray();

        Assert.That(orphans, Is.Empty, "Remove dead check definitions, or reference them from a path rule.");
    }

    private static JsonDocument ReadAgentUpJson(string root)
        => JsonDocument.Parse(File.ReadAllText(Path.Join(root, "agent-up.json")));

    private static IReadOnlyList<VerificationRuleShape> PathRules(string root)
    {
        using var document = ReadAgentUpJson(root);
        return
        [
            .. document.RootElement.GetProperty("verification").GetProperty("paths").EnumerateArray()
                .Select(rule => new VerificationRuleShape(
                    rule.GetProperty("match").GetString() ?? string.Empty,
                    [.. rule.GetProperty("checks").EnumerateArray().Select(check => check.GetString() ?? string.Empty)]))
        ];
    }

    /// <summary>
    /// Whether a rule glob addresses files inside a project directory. Only the
    /// "&lt;Project&gt;/**" shape counts: a repository-wide opt-out such as "**/*.md" must
    /// not be mistaken for coverage of a project.
    /// </summary>
    private static bool MatchesProject(string match, string project)
        => match.Equals(project + "/**", StringComparison.Ordinal)
           || match.StartsWith(project + "/", StringComparison.Ordinal);
}
