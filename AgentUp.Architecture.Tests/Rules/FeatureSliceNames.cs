using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class FeatureSliceNames
{
    [Test]
    public void Feature_names_are_customer_operator_or_maintainer_capabilities()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var forbiddenSliceNames = ArchitectureFixture.AllowedFeatureTypeFolders
            .Concat(ArchitectureFixture.AllowedSharedTypeFolders)
            .ToHashSet(StringComparer.Ordinal);

        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: ArchitectureFixture.Parts(root, path)))
            .Select(item => (item.Path, Slice: FeatureSlice(item.Parts)))
            .Where(item => item.Slice is not null && forbiddenSliceNames.Contains(item.Slice))
            .Select(item => $"{ArchitectureFixture.Relative(root, item.Path)} uses technical type-folder name '{item.Slice}' as a feature slice")
            .Distinct()
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Feature slice names must describe product, customer, operator, or maintainer capabilities.");
    }

    private static string? FeatureSlice(string[] parts)
        => parts.Length >= 4 && parts[1] == "Features" ? parts[2] : null;
}
