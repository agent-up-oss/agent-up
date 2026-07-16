using System.Text.RegularExpressions;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts;

[TestFixture]
public class PackagingSliceBoundaryTests
{
    private static readonly Regex CrossSliceUsingPattern = new(
        @"^using AgentUp\.Packaging\.Features\.(?<slice>[^.]+)\.(?<folder>Services|Models|Providers|Interfaces|Factories);$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Test]
    public void FeatureSlicesDoNotReachIntoOtherSlicesInternals()
    {
        var sourceRoot = FindPackagingSourceRoot();
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
    public void ProgramUsesCompositionRootAndFeatureControllerEntrypoint()
    {
        var sourceRoot = FindPackagingSourceRoot();
        var programSource = File.ReadAllText(Path.Join(sourceRoot, "Program.cs"));
        var featureUsings = Regex.Matches(programSource, @"^using AgentUp\.Packaging\.Features\.[^;]+;$", RegexOptions.Multiline)
            .Select(match => match.Value)
            .ToArray();

        Assert.That(featureUsings, Is.Empty);
        Assert.That(programSource, Does.Contain("using AgentUp.Packaging.Shared.Factories;"));
        Assert.That(programSource, Does.Contain(".PackageCommands.ExecuteAsync(args)"));
    }

    [Test]
    public void ControllersDoNotInstantiateDependencies()
    {
        var sourceRoot = FindPackagingSourceRoot();
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

    private static string FindPackagingSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Join(directory.FullName, "AgentUp.Packaging");
            if (File.Exists(Path.Join(candidate, "AgentUp.Packaging.csproj")))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find AgentUp.Packaging source root.");
    }
}
