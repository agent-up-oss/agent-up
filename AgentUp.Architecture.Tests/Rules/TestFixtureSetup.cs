using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// A rule against new fixtures assembling their subject somewhere other than the test.
/// </summary>
/// <remarks>
/// A <c>[SetUp]</c> method moves the arrangement out of the test that depends on it, so
/// reading the test no longer tells you what the subject was given, and every test in the
/// fixture pays for whatever the slowest one needed. The alternative is a local helper the
/// test calls with explicit arguments, which reads top to bottom and lets one test differ
/// without a new shared method. <c>AgentUp.Verification.Tests</c> was written that way and
/// is the worked example.
/// <para>
/// The baseline is a ratchet, not an allowlist: an entry whose fixture no longer has a
/// setup method fails the suite, so burning down debt means deleting the line and the file
/// stays an honest picture of what is left. New fixtures are not added to it.
/// </para>
/// <para>
/// <c>[TearDown]</c> is untouched. Releasing a temp directory or a host has no bearing on
/// what a test can be read to say.
/// </para>
/// </remarks>
[TestFixture]
public sealed class TestFixtureSetup
{
    private const string Baseline = "AgentUp.Architecture.Tests/Baselines/test-fixture-setup-debt.txt";

    private static readonly string[] SetUpAttributes = ["SetUp", "OneTimeSetUp"];

    [Test]
    public void New_fixtures_arrange_their_subject_in_the_test_rather_than_a_setup_method()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var baseline = ArchitectureFixture.LoadBaseline(root, Baseline);
        TestContext.Out.WriteLine($"Fixture setup debt: {baseline.Count} entry(ies).");

        var violations = FixturesWithSetUp(root)
            .Where(fixture => !baseline.Contains(fixture))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            "A new fixture must arrange its subject inside the test, through a local helper taking "
            + $"explicit parameters. Move the setup, or add the entry to {Baseline}.");
    }

    [Test]
    public void The_setup_baseline_holds_no_entry_that_is_already_clean()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var baseline = ArchitectureFixture.LoadBaseline(root, Baseline);
        var withSetUp = FixturesWithSetUp(root).ToHashSet(StringComparer.Ordinal);

        var stale = baseline
            .Where(entry => !withSetUp.Contains(entry))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(stale, Is.Empty,
            $"These fixtures no longer have a setup method. Delete their lines from {Baseline}.");
    }

    private static IEnumerable<string> FixturesWithSetUp(string root)
        => ArchitectureFixture.TestSourceFiles(root)
            .Where(HasSetUpMethod)
            .Select(path => ArchitectureFixture.Relative(root, path).Replace('\\', '/'));

    private static bool HasSetUpMethod(string path)
    {
        var (_, node) = ArchitectureFixture.ParseSourceFile(path);

        return node.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .SelectMany(method => method.AttributeLists.SelectMany(list => list.Attributes))
            .Any(attribute => SetUpAttributes.Contains(
                ArchitectureFixture.FinalTypeSegment(attribute.Name).Replace("Attribute", string.Empty),
                StringComparer.Ordinal));
    }
}
