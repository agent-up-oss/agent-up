using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class SourceFileLayout
{
    [Test]
    public void Production_source_files_live_under_features_shared_or_entrypoints()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => !IsAllowedProductionRootFile(root, path))
            .Where(path => !ArchitectureFixture.HasPathPart(root, path, "Features") && !ArchitectureFixture.HasPathPart(root, path, "Shared"))
            .Select(path => ArchitectureFixture.Relative(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Production files must live under Features/, Shared/, or an approved project entrypoint path.");
    }

    private static bool IsAllowedProductionRootFile(string root, string path)
    {
        var parts = ArchitectureFixture.Parts(root, path);
        return parts.Length == 2 && parts[1] is "Program.cs" or "App.axaml.cs"
               || parts.Length == 3 && parts[1] == "Properties" && parts[2] == "AssemblyInfo.cs";
    }
}
