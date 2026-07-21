using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class SharedTypeFolders
{
    [Test]
    public void Shared_source_files_live_in_approved_type_folders()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: ArchitectureFixture.Parts(root, path)))
            .Where(item => TrySharedTypeFolder(item.Parts, out var folder) && !ArchitectureFixture.AllowedSharedTypeFolders.Contains(folder))
            .Select(item => $"{ArchitectureFixture.Relative(root, item.Path)} uses unsupported shared type folder '{SharedTypeFolder(item.Parts)}'")
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"Shared files must use only these type folders: {string.Join(", ", ArchitectureFixture.AllowedSharedTypeFolders)}.");
    }

    private static bool TrySharedTypeFolder(string[] parts, out string folder)
    {
        if (parts.Length < 3 || parts[1] != "Shared")
        {
            folder = "";
            return false;
        }
        folder = parts.Length >= 4 ? parts[2] : "(none)";
        return true;
    }

    private static string SharedTypeFolder(string[] parts)
        => parts.Length >= 4 ? parts[2] : "(none)";
}
