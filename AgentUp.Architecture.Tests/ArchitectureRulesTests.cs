using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AgentUp.Architecture.Tests;

[TestFixture]
public sealed class ArchitectureRulesTests
{
    private static readonly string[] ProductionProjects =
    [
        "AgentUp.Server",
        "AgentUp.Capabilities.Abstractions",
        "AgentUp.Capabilities.Common",
        "AgentUp.Capabilities.Dotnet",
        "AgentUp.Capabilities.Docker",
        "AgentUp.Desktop",
        "AgentUp.CLI",
        "AgentUp.Installers",
        "AgentUp.InstallerApp",
        "AgentUp.Packaging",
        "AgentUp.PackageSmoke"
    ];

    private static readonly string[] TestProjects =
    [
        "AgentUp.Server.Tests",
        "AgentUp.Capabilities.Abstractions.Tests",
        "AgentUp.Capabilities.Common.Tests",
        "AgentUp.Capabilities.Dotnet.Tests",
        "AgentUp.Capabilities.Docker.Tests",
        "AgentUp.Desktop.Tests",
        "AgentUp.CLI.Tests",
        "AgentUp.Installers.Tests",
        "AgentUp.InstallerApp.Tests",
        "AgentUp.Packaging.Tests",
        "AgentUp.PackageSmoke.Tests",
        "AgentUp.Tests",
        "AgentUp.Architecture.Tests"
    ];

    private static readonly string[] AllowedFeatureTypeFolders =
    [
        "Controllers",
        "DTOs",
        "Factories",
        "Interfaces",
        "Models",
        "Providers",
        "Repositories",
        "Services",
        "Resources",
        "Tools",
        "ViewModels",
        "Views"
    ];

    private static readonly string[] AllowedSharedTypeFolders =
    [
        "Factories",
        "Interfaces",
        "Providers"
    ];

    private static readonly string[] AllowedTestKindFolders =
    [
        "Architecture",
        "Automation",
        "Commands",
        "E2E",
        "Fake",
        "Headless",
        "HTTP",
        "Http",
        "Mcp",
        "Provider",
        "Repository",
        "Resources",
        "Support",
        "TerminalIntegration",
        "Tools",
        "Unit"
    ];

    private static readonly string[] ForbiddenUnitTestTokens =
    [
        "File.",
        "Directory.",
        "Path.GetTempPath",
        "Process.Start",
        "new ProcessStartInfo",
        "Directory.SetCurrentDirectory",
        "Environment.SetEnvironmentVariable",
        "TcpListener",
        "TcpClient",
        "Socket"
    ];

    private static readonly Regex ConstructionInController = new(
        @"new\s+\w*(Service|Provider|Repository|Factory)\s*\(",
        RegexOptions.Compiled);

    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader().LoadAssemblies(
        ProductionProjects.Select(System.Reflection.Assembly.Load).ToArray()).Build();

    [Test]
    public void Production_project_dependencies_follow_ownership_boundaries()
    {
        AssertDoesNotDependOn("AgentUp.Capabilities.Abstractions", Except("AgentUp.Capabilities.Abstractions"));
        AssertDoesNotDependOn("AgentUp.Capabilities.Common", Except("AgentUp.Capabilities.Common", "AgentUp.Capabilities.Abstractions"));
        AssertDoesNotDependOn("AgentUp.Capabilities.Dotnet", Except("AgentUp.Capabilities.Dotnet", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Common"));
        AssertDoesNotDependOn("AgentUp.Capabilities.Docker", Except("AgentUp.Capabilities.Docker", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Common"));
        AssertDoesNotDependOn("AgentUp.Server", Except("AgentUp.Server", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Dotnet", "AgentUp.Capabilities.Docker"));
        AssertDoesNotDependOn("AgentUp.Desktop", Except("AgentUp.Desktop"));
        AssertDoesNotDependOn("AgentUp.CLI", Except("AgentUp.CLI", "AgentUp.Capabilities.Abstractions"));
        AssertDoesNotDependOn("AgentUp.Installers", Except("AgentUp.Installers"));
        AssertDoesNotDependOn("AgentUp.InstallerApp", Except("AgentUp.InstallerApp", "AgentUp.Installers", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Common"));
        AssertDoesNotDependOn("AgentUp.Packaging", Except("AgentUp.Packaging", "AgentUp.Installers"));
        AssertDoesNotDependOn("AgentUp.PackageSmoke", Except("AgentUp.PackageSmoke", "AgentUp.Installers"));
    }

    [Test]
    public void Production_source_files_live_under_features_shared_or_entrypoints()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ProductionSourceFiles(root)
            .Where(path => !IsAllowedProductionRootFile(root, path))
            .Where(path => !HasPathPart(root, path, "Features") && !HasPathPart(root, path, "Shared"))
            .Select(path => Relative(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Production files must live under Features/, Shared/, or an approved project entrypoint path.");
    }

    [Test]
    public void Feature_source_files_live_in_approved_type_folders()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: Parts(root, path)))
            .Where(item => TryFeatureTypeFolder(item.Parts, out var folder) && !AllowedFeatureTypeFolders.Contains(folder))
            .Select(item => $"{Relative(root, item.Path)} uses unsupported feature type folder '{FeatureTypeFolder(item.Parts)}'")
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"Feature files must use only these type folders: {string.Join(", ", AllowedFeatureTypeFolders)}.");
    }

    [Test]
    public void Shared_source_files_live_in_approved_type_folders()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: Parts(root, path)))
            .Where(item => TrySharedTypeFolder(item.Parts, out var folder) && !AllowedSharedTypeFolders.Contains(folder))
            .Select(item => $"{Relative(root, item.Path)} uses unsupported shared type folder '{SharedTypeFolder(item.Parts)}'")
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"Shared files must use only these type folders: {string.Join(", ", AllowedSharedTypeFolders)}.");
    }

    [Test]
    public void Feature_names_are_customer_operator_or_maintainer_capabilities()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var forbiddenSliceNames = AllowedFeatureTypeFolders.Concat(AllowedSharedTypeFolders).ToHashSet(StringComparer.Ordinal);
        var violations = ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: Parts(root, path)))
            .Select(item => (item.Path, Slice: FeatureSlice(item.Parts)))
            .Where(item => item.Slice is not null && forbiddenSliceNames.Contains(item.Slice))
            .Select(item => $"{Relative(root, item.Path)} uses technical type-folder name '{item.Slice}' as a feature slice")
            .Distinct()
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Feature slice names must describe product, customer, operator, or maintainer capabilities.");
    }

    [Test]
    public void Controllers_do_not_construct_services_providers_repositories_or_factories()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ProductionSourceFiles(root)
            .Where(path => HasPathPart(root, path, "Controllers"))
            .SelectMany(path => MatchingLines(root, path, ConstructionInController))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers must receive dependencies through constructors and map DTO calls to services.");
    }

    [Test]
    public void Test_projects_use_feature_sliced_test_kind_folders()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = TestSourceFiles(root)
            .Where(path => !IsAllowedTestSupportFile(root, path))
            .Select(path => (Path: path, Parts: Parts(root, path)))
            .Where(item => !IsFeatureSlicedTest(item.Parts))
            .Select(item => Relative(root, item.Path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Tests must live under Features/<Slice>/, Features/<Slice>/<TestKind>/, Fake/, Support/, E2E/, Fixtures/, or Architecture/.");
    }

    [Test]
    public void Unit_tests_do_not_use_real_io_process_socket_or_environment_mutation()
    {
        var root = FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = TestSourceFiles(root)
            .Where(path => HasPathPart(root, path, "Unit"))
            .SelectMany(path => ForbiddenUnitTestTokens
                .Where(File.ReadAllText(path).Contains)
                .Select(token => $"{Relative(root, path)} contains {token}"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Tests that use real filesystem, process, socket, current-directory, or environment mutation APIs must not live in Unit folders.");
    }

    private static void AssertDoesNotDependOn(string sourceAssembly, IReadOnlyCollection<string> allowedAssemblies)
    {
        var source = Types().That().ResideInAssembly(sourceAssembly).As(sourceAssembly);

        foreach (var forbiddenAssembly in ProductionProjects.Except(allowedAssemblies))
        {
            var forbidden = Types().That().ResideInAssembly(forbiddenAssembly).As(forbiddenAssembly);
            IArchRule rule = Types().That().Are(source).Should().NotDependOnAny(forbidden)
                .Because($"{sourceAssembly} must not take runtime dependencies on {forbiddenAssembly}")
                .WithoutRequiringPositiveResults();
            rule.Check(Architecture);
        }
    }

    private static string[] Except(params string[] allowed)
        => allowed;

    private static IEnumerable<string> ProductionSourceFiles(string repositoryRoot)
        => ProductionProjects.SelectMany(project => ProjectSourceFiles(repositoryRoot, project));

    private static IEnumerable<string> TestSourceFiles(string repositoryRoot)
        => TestProjects
            .Where(project => Directory.Exists(Path.Combine(repositoryRoot, project)))
            .SelectMany(project => ProjectSourceFiles(repositoryRoot, project));

    private static IEnumerable<string> ProjectSourceFiles(string repositoryRoot, string project)
        => Directory.EnumerateFiles(Path.Combine(repositoryRoot, project), "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasPathPart(repositoryRoot, path, "bin") && !HasPathPart(repositoryRoot, path, "obj"));

    private static bool IsAllowedProductionRootFile(string root, string path)
    {
        var parts = Parts(root, path);
        return parts.Length == 2 && parts[1] is "Program.cs" or "App.axaml.cs"
               || parts.Length == 3 && parts[1] == "Properties" && parts[2] == "AssemblyInfo.cs";
    }

    private static bool IsAllowedTestSupportFile(string root, string path)
    {
        var parts = Parts(root, path);
        return parts.Length == 2 && parts[1] == "Program.cs"
               || parts.Length == 2 && parts[0] == "AgentUp.Architecture.Tests"
               || parts.Length >= 2 && AllowedTestKindFolders.Contains(parts[1])
               || parts.Length >= 2 && parts[1] == "Fixtures"
               || parts.Length >= 3 && parts[1] == "Properties";
    }

    private static bool IsFeatureSlicedTest(string[] parts)
    {
        if (parts.Length < 4 || parts[1] != "Features")
            return false;

        return parts.Length == 4 || AllowedTestKindFolders.Contains(parts[3]);
    }

    private static bool TryFeatureTypeFolder(string[] parts, out string folder)
    {
        folder = FeatureTypeFolder(parts) ?? "";
        return folder.Length > 0;
    }

    private static string? FeatureTypeFolder(string[] parts)
        => parts.Length >= 5 && parts[1] == "Features" ? parts[3] : null;

    private static string? FeatureSlice(string[] parts)
        => parts.Length >= 4 && parts[1] == "Features" ? parts[2] : null;

    private static bool TrySharedTypeFolder(string[] parts, out string folder)
    {
        folder = SharedTypeFolder(parts) ?? "";
        return folder.Length > 0;
    }

    private static string? SharedTypeFolder(string[] parts)
        => parts.Length >= 4 && parts[1] == "Shared" ? parts[2] : null;

    private static IEnumerable<string> MatchingLines(string root, string path, Regex regex)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (regex.IsMatch(line))
                yield return $"{Relative(root, path)}:{lineNumber}: {line.Trim()}";
        }
    }

    private static bool HasPathPart(string root, string path, string part)
        => Parts(root, path).Contains(part, StringComparer.Ordinal);

    private static string[] Parts(string root, string path)
        => Relative(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string Relative(string root, string path)
        => Path.GetRelativePath(root, path);

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
