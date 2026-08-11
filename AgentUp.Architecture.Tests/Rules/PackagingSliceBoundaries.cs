using System.Text.RegularExpressions;
using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class PackagingSliceBoundaries
{
    private static readonly Regex CrossSliceUsingPattern = new(
        @"^using LocalInstaller\.Packaging\.Features\.(?<slice>[^.]+)\.(?<folder>Services|Models|Providers|Interfaces|Factories);$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Test]
    public void FeatureSlicesDoNotReachIntoOtherSlicesInternals()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var sourceRoot = Path.Join(root, "LocalInstaller.Packaging");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Join(sourceRoot, "Features"), "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length < 2)
                continue;

            var currentSlice = parts[1];
            var source = File.ReadAllText(file);
            foreach (Match match in CrossSliceUsingPattern.Matches(source))
            {
                var importedSlice = match.Groups["slice"].Value;
                if (importedSlice == currentSlice)
                    continue;

                violations.Add($"{relativePath} imports {match.Value}");
            }
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    [Test]
    public void PackagingLibraryDoesNotOwnAProductEntrypoint()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var sourceRoot = Path.Join(root, "LocalInstaller.Packaging");

        Assert.That(File.Exists(Path.Join(sourceRoot, "Program.cs")), Is.False);
    }

    [Test]
    public void ControllersDoNotInstantiateDependencies()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var sourceRoot = Path.Join(root, "LocalInstaller.Packaging");
        var violations = Directory
            .EnumerateFiles(Path.Join(sourceRoot, "Features"), "*.cs", SearchOption.AllDirectories)
            .Where(file => file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Controllers"))
            .Where(file => !Path.GetFileName(file).StartsWith("I", StringComparison.Ordinal))
            .Select(file => (File: file, Source: File.ReadAllText(file)))
            .Where(file => Regex.IsMatch(file.Source, @"\bnew\s+[A-Z][A-Za-z0-9_]*\s*\("))
            .Select(file => Path.GetRelativePath(sourceRoot, file.File))
            .ToArray();

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }
}
