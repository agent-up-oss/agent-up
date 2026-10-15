using System.Text.RegularExpressions;
using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Structural coverage for Agent-Up Mobile feature slices. Component screens are
/// exempt: product UI decisions belong in providers, and the suite does not take a
/// React Native rendering library.
/// </summary>
[TestFixture]
public sealed class MobileFeatureTestCoverage
{
    private static readonly Regex TestCall = new(@"^\s*test\(", RegexOptions.Compiled);

    [Test]
    public void Mobile_features_with_logic_have_tests()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var missing = FeatureDirectories(root)
            .Where(feature => HasLogic(feature) && CountTests(feature) == 0)
            .Select(feature => $"{ArchitectureFixture.Relative(root, feature)} has production TypeScript outside components/ but no *.test.ts")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(missing, Is.Empty,
            "A new AgentUp.Mobile/src/features/<slice> with providers, controllers, or hooks must ship tests in that slice.");
    }

    [Test]
    public void Mobile_provider_folders_have_sibling_tests()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var missing = ProviderDirectories(root)
            .Where(providers => HasProductionTypeScript(providers) && CountTests(providers) == 0)
            .Select(providers => $"{ArchitectureFixture.Relative(root, providers)} has production files but no *.test.ts")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(missing, Is.Empty,
            "A providers/ folder under AgentUp.Mobile/src/features must have sibling tests so recursive discovery can run them.");
    }

    [Test]
    public void Mobile_features_with_logic_hold_more_than_a_token_test()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var thin = FeatureDirectories(root)
            .Where(HasLogic)
            .Select(feature => (Feature: feature, Tests: CountTests(feature)))
            .Where(item => item.Tests is > 0 and < 2)
            .Select(item => $"{ArchitectureFixture.Relative(root, item.Feature)} has {item.Tests} test(s)")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(thin, Is.Empty,
            "A mobile feature with logic needs at least two tests so a single case cannot stand in for the slice.");
    }

    private static IEnumerable<string> FeatureDirectories(string root)
        => Directory.GetDirectories(Path.Join(root, "AgentUp.Mobile", "src", "features"));

    private static IEnumerable<string> ProviderDirectories(string root)
        => FeatureDirectories(root)
            .Select(feature => Path.Join(feature, "providers"))
            .Where(Directory.Exists);

    private static bool HasLogic(string featureDirectory)
        => TypeScriptFiles(featureDirectory)
            .Any(path => !IsTest(path) && !ArchitectureFixture.HasPathPart(featureDirectory, path, "components"));

    private static bool HasProductionTypeScript(string directory)
        => TypeScriptFiles(directory).Any(path => !IsTest(path));

    private static int CountTests(string directory)
        => TypeScriptFiles(directory)
            .Where(IsTest)
            .Sum(path => File.ReadAllLines(path).Count(line => TestCall.IsMatch(line)));

    private static IEnumerable<string> TypeScriptFiles(string directory)
        => Directory.EnumerateFiles(directory, "*.ts", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(directory, "*.tsx", SearchOption.AllDirectories));

    private static bool IsTest(string path)
        => path.EndsWith(".test.ts", StringComparison.Ordinal)
           || path.EndsWith(".test.tsx", StringComparison.Ordinal);
}
