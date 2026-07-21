using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class FeatureTypeFolders
{
    [Test]
    public void Feature_source_files_live_in_approved_type_folders()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: ArchitectureFixture.Parts(root, path)))
            .Where(item => TryFeatureTypeFolder(item.Parts, out var folder) && !ArchitectureFixture.AllowedFeatureTypeFolders.Contains(folder))
            .Select(item => $"{ArchitectureFixture.Relative(root, item.Path)} uses unsupported feature type folder '{FeatureTypeFolder(item.Parts)}'")
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"Feature files must use only these type folders: {string.Join(", ", ArchitectureFixture.AllowedFeatureTypeFolders)}.");
    }

    private static bool TryFeatureTypeFolder(string[] parts, out string folder)
    {
        folder = FeatureTypeFolder(parts) ?? "";
        return folder.Length > 0;
    }

    private static string? FeatureTypeFolder(string[] parts)
        => parts.Length >= 5 && parts[1] == "Features" ? parts[3] : null;
}
