using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Rules tying each production type folder to the test-kind folder that covers it.
/// </summary>
/// <remarks>
/// These are structural: they check that tests exist where they belong and that there is
/// more than one of them. That is deliberately weak - a test count is crude and gameable -
/// and it is all this suite can assert soundly, because it runs before the test suites that
/// produce coverage reports. Whether a slice is actually covered is measured by
/// `agentup verify slices`, which runs after them and reads the reports.
/// <para>
/// Both baselines are ratchets, not allowlists: an entry that is already satisfied fails
/// the suite, so burning down debt means deleting the line, and the files stay an honest
/// picture of what is left.
/// </para>
/// </remarks>
[TestFixture]
public sealed class SliceTestCoverage
{
    private const string MissingCoverageBaseline =
        "AgentUp.Architecture.Tests/Baselines/slice-test-coverage-debt.txt";

    private const string ThinFolderBaseline =
        "AgentUp.Architecture.Tests/Baselines/thin-test-folder-debt.txt";

    /// <summary>A single test does not cover a type folder, whatever the rule's name says.</summary>
    private const int MinimumTestsPerFolder = 2;

    private static readonly (string ProductionFolder, string TestKind)[] RequiredCoverage =
    [
        ("Controllers", "Controller"),
        ("Services", "Unit"),
        ("Models", "Unit"),
        ("Providers", "Provider")
    ];

    [Test]
    public void Feature_slices_have_matching_controller_unit_and_provider_test_coverage()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var baseline = ArchitectureFixture.LoadBaseline(root, MissingCoverageBaseline);
        TestContext.Out.WriteLine($"Slice test coverage debt: {baseline.Count} entry(ies).");

        var violations = RequiredPairings(root)
            .Where(pairing => pairing.TestFiles.Count == 0)
            .Select(MissingDescription)
            .Where(violation => !baseline.Contains(violation))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Feature slices with Controllers, Services/Models, or Providers must have matching "
            + $"Controller, Unit, or Provider tests. Add them, or add the entry to {MissingCoverageBaseline}.");
    }

    [Test]
    public void Required_test_folders_hold_more_than_a_token_test()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var baseline = ArchitectureFixture.LoadBaseline(root, ThinFolderBaseline);
        TestContext.Out.WriteLine($"Thin test folder debt: {baseline.Count} entry(ies).");

        var violations = RequiredPairings(root)
            .Where(pairing => pairing.TestFiles.Count > 0)
            .Select(pairing => (Pairing: pairing, Tests: CountTests(pairing.TestFiles)))
            .Where(item => item.Tests < MinimumTestsPerFolder)
            .Select(item => ThinDescription(item.Pairing, item.Tests))
            .Where(violation => !baseline.Contains(violation))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"A required test-kind folder must hold at least {MinimumTestsPerFolder} tests. One test "
            + "satisfies the structural rule without covering the slice.");
    }

    [Test]
    public void Baselines_contain_no_entries_that_are_already_satisfied()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var pairings = RequiredPairings(root).ToArray();

        var live = pairings
            .Where(pairing => pairing.TestFiles.Count == 0)
            .Select(MissingDescription)
            .Concat(pairings
                .Where(pairing => pairing.TestFiles.Count > 0)
                .Select(pairing => (Pairing: pairing, Tests: CountTests(pairing.TestFiles)))
                .Where(item => item.Tests < MinimumTestsPerFolder)
                .Select(item => ThinDescription(item.Pairing, item.Tests)))
            .ToHashSet(StringComparer.Ordinal);

        var resolved = ArchitectureFixture.LoadBaseline(root, MissingCoverageBaseline)
            .Concat(ArchitectureFixture.LoadBaseline(root, ThinFolderBaseline))
            .Where(entry => !live.Contains(entry))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(resolved, Is.Empty,
            "These baseline entries no longer describe a violation. Delete them: a baseline that "
            + "outlives the debt it records stops being a measure of what is left.");
    }

    /// <summary>
    /// Every production type folder that has code, paired with the test files in the
    /// test-kind folder that is supposed to cover it.
    /// </summary>
    private static IEnumerable<Pairing> RequiredPairings(string root)
        => ArchitectureFixture.ProductionProjects
            .Where(project => Directory.Exists(Path.Join(root, project, "Features")))
            .Where(project => Directory.Exists(Path.Join(root, project + ".Tests")))
            .SelectMany(project => PairingsOf(root, project));

    private static IEnumerable<Pairing> PairingsOf(string root, string project)
    {
        var testProject = project + ".Tests";

        foreach (var sliceDirectory in Directory.EnumerateDirectories(Path.Join(root, project, "Features")))
        {
            var slice = Path.GetFileName(sliceDirectory);
            foreach (var (productionFolder, testKind) in RequiredCoverage)
            {
                var productionPath = Path.Join(sliceDirectory, productionFolder);
                if (!HasSource(productionPath))
                    continue;

                var testPath = Path.Join(root, testProject, "Features", slice, testKind);
                yield return new Pairing(project, testProject, slice, productionFolder, testKind, TestFilesIn(testPath));
            }
        }
    }

    private static bool HasSource(string directory)
        => Directory.Exists(directory)
           && Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Any();

    private static IReadOnlyList<string> TestFilesIn(string directory)
        => Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*Tests.cs", SearchOption.AllDirectories).ToArray()
            : [];

    /// <summary>
    /// Counts declared test cases rather than test methods, so a method with ten
    /// [TestCase] rows reads as the ten tests it is, while a [TestCaseSource] - whose rows
    /// are only known at run time - counts as one.
    /// </summary>
    private static int CountTests(IReadOnlyList<string> testFiles)
        => testFiles.Sum(path => ArchitectureFixture.ParseSourceFile(path).Root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .SelectMany(method => method.AttributeLists.SelectMany(list => list.Attributes))
            .Count(attribute => ArchitectureFixture.FinalTypeSegment(attribute.Name)
                is "Test" or "TestCase" or "TestCaseSource"));

    private static string MissingDescription(Pairing pairing)
        => $"{pairing.Project}/Features/{pairing.Slice}/{pairing.ProductionFolder} requires "
           + $"{pairing.TestProject}/Features/{pairing.Slice}/{pairing.TestKind}/*Tests.cs";

    private static string ThinDescription(Pairing pairing, int tests)
        => $"{pairing.TestProject}/Features/{pairing.Slice}/{pairing.TestKind} covers "
           + $"{pairing.Project}/Features/{pairing.Slice}/{pairing.ProductionFolder} with {tests} test"
           + (tests == 1 ? string.Empty : "s");

    private sealed record Pairing(
        string Project,
        string TestProject,
        string Slice,
        string ProductionFolder,
        string TestKind,
        IReadOnlyList<string> TestFiles);
}
