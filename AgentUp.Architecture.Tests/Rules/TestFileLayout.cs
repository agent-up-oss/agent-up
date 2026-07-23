using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class TestFileLayout
{
    [Test]
    public void Test_projects_use_feature_sliced_test_kind_folders()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.TestSourceFiles(root)
            .Where(path => !IsAllowedTestSupportFile(root, path))
            .Select(path => (Path: path, Parts: ArchitectureFixture.Parts(root, path)))
            .Where(item => !IsFeatureSlicedTest(item.Parts))
            .Select(item => ArchitectureFixture.Relative(root, item.Path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Tests must live under Features/<Slice>/<TestKind>/, Fake/, Support/, E2E/, Fixtures/, or Architecture/.");
    }

    private static bool IsAllowedTestSupportFile(string root, string path)
    {
        var parts = ArchitectureFixture.Parts(root, path);
        return parts.Length == 2 && parts[1] == "Program.cs"
               || parts[0] == "AgentUp.Architecture.Tests"
               || parts.Length >= 2 && ArchitectureFixture.AllowedRootTestSupportFolders.Contains(parts[1])
               || parts.Length >= 3 && parts[1] == "Properties";
    }

    private static bool IsFeatureSlicedTest(string[] parts)
    {
        if (parts.Length < 4 || parts[1] != "Features")
            return false;

        return parts.Length >= 5 && ArchitectureFixture.AllowedTestKindFolders.Contains(parts[3]);
    }
}
