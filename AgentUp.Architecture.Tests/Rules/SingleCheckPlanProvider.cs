using System.Text.RegularExpressions;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// One resolver, two hosts. <c>au-check</c> and <c>agent-up verify</c> must answer from the
/// same <c>CheckPlanProvider</c>. A second implementation would make the portable plan and
/// the CI plan disagree.
/// </summary>
[TestFixture]
public sealed class SingleCheckPlanProvider
{
    [Test]
    public void The_solution_declares_CheckPlanProvider_once()
    {
        var root = Fixtures.ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var declarations = SourceFiles(root)
            .Where(path => CheckPlanProviderDeclaration.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(declarations, Is.EqualTo(new[] { Path.Join("AgentUp.Verification", "Features", "Verification", "Providers", "CheckPlanProvider.cs") }));
    }

    [Test]
    public void AuCheck_does_not_resolve_checks()
    {
        var root = Fixtures.ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var project = Path.Join(root, "AgentUp.AUCheck");
        var hits = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("CheckPlanProvider", StringComparison.Ordinal)
                       || text.Contains("CreatePlan", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.That(hits, Is.Empty, "AgentUp.AUCheck hosts the library. It does not decide which checks are required.");
    }

    private static readonly Regex CheckPlanProviderDeclaration = new(
        @"\bclass CheckPlanProvider\b",
        RegexOptions.CultureInvariant);

    private static IEnumerable<string> SourceFiles(string root)
        => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
