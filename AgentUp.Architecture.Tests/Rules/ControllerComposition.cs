using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ControllerComposition
{
    private static readonly string[] InternalTypeFolders =
    [
        "Services",
        "Providers",
        "Repositories",
        "Factories",
        "Interfaces",
        "Models"
    ];

    [Test]
    public void Controllers_do_not_construct_services_providers_repositories_or_factories()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var typeSuffixes = new[] { "Service", "Provider", "Repository", "Factory" };
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Controllers"))
            .SelectMany(path => ArchitectureFixture.FindConstructionsInFile(root, path, typeSuffixes))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers must receive dependencies through constructors and map DTO calls to services.");
    }

    [Test]
    public void Controllers_are_not_static_composition_wrappers()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Controllers"))
            .SelectMany(path =>
            {
                var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
                return rootNode.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(type => type.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal))
                    .Where(type => type.Modifiers.Any(modifier => modifier.RawKind == (int)SyntaxKind.StaticKeyword))
                    .Select(type => $"{ArchitectureFixture.Location(root, path, tree, type)}: static {type.Identifier.Text}");
            })
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers must be constructor-injected boundaries, not static factories or project composition wrappers.");
    }

    [Test]
    public void Controllers_do_not_return_internal_slice_implementation_types()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Controllers"))
            .SelectMany(path =>
            {
                var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
                return rootNode.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method => ReturnTypeLooksInternal(method.ReturnType))
                    .Select(method => $"{ArchitectureFixture.Location(root, path, tree, method)}: returns {method.ReturnType}");
            })
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers may return DTOs, primitives, framework results, or controller-boundary types, but not Services/Providers/Repositories/Factories/Interfaces/Models.");
    }

    [Test]
    public void Controllers_do_not_import_low_level_implementation_folders()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var lowLevelFolders = new[] { "Providers", "Repositories", "Factories" };
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Controllers"))
            .SelectMany(path => FindLowLevelControllerUsings(root, path, lowLevelFolders))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers must route DTOs to domain services; providers, repositories, and factories stay behind services or composition roots.");
    }

    [Test]
    public void Feature_slices_with_inbound_same_project_traffic_have_controller_boundaries()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: ArchitectureFixture.Parts(root, path)))
            .Where(item => TryFeatureLocation(item.Parts, out _, out _, out _)
                           || IsApprovedEntrypoint(item.Parts))
            .SelectMany(item => FindInboundFeatureTargets(root, item.Path, item.Parts))
            .Distinct()
            .Where(target => !FeatureHasController(root, target.Project, target.Slice))
            .Select(target => $"{target.Project}/Features/{target.Slice} receives same-project inbound traffic but has no Controllers/ boundary")
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Feature slices that receive inbound traffic from entrypoints or sibling slices must expose a Controllers/ boundary.");
    }

    private static bool ReturnTypeLooksInternal(TypeSyntax returnType)
    {
        var text = returnType.ToString();
        return InternalTypeFolders.Any(folder => text.Contains($".{folder}.", StringComparison.Ordinal))
               || InternalTypeFolders.Any(folder => ArchitectureFixture.FinalTypeSegment(returnType).EndsWith(folder[..^1], StringComparison.Ordinal));
    }

    private static IEnumerable<string> FindLowLevelControllerUsings(string root, string path, IReadOnlyCollection<string> lowLevelFolders)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        foreach (var usingDirective in rootNode.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var name = usingDirective.Name?.ToString();
            if (name is null)
                continue;

            if (lowLevelFolders.Any(folder => name.Contains($".{folder}", StringComparison.Ordinal)))
                yield return $"{ArchitectureFixture.Location(root, path, tree, usingDirective)}: using {name}";
        }
    }

    private static IEnumerable<(string Project, string Slice)> FindInboundFeatureTargets(string root, string path, string[] sourceParts)
    {
        var sourceIsFeature = TryFeatureLocation(sourceParts, out var sourceProject, out var sourceSlice, out _);
        if (!sourceIsFeature && !IsApprovedEntrypoint(sourceParts))
            yield break;

        var (_, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        var project = sourceIsFeature ? sourceProject : sourceParts[0];
        var prefix = $"{project}.Features.";

        foreach (var usingDirective in rootNode.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var name = usingDirective.Name?.ToString();
            if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var targetParts = name[prefix.Length..].Split('.');
            if (targetParts.Length < 1)
                continue;

            var targetSlice = targetParts[0];
            if (sourceIsFeature && targetSlice == sourceSlice)
                continue;

            yield return (project, targetSlice);
        }
    }

    private static bool FeatureHasController(string root, string project, string slice)
    {
        var controllers = Path.Join(root, project, "Features", slice, "Controllers");
        return Directory.Exists(controllers)
               && Directory.EnumerateFiles(controllers, "*.cs", SearchOption.TopDirectoryOnly).Any();
    }

    private static bool IsApprovedEntrypoint(string[] parts)
        => parts.Length == 2
           && parts[1] is "Program.cs" or "App.axaml.cs";

    private static bool TryFeatureLocation(string[] parts, out string project, out string slice, out string typeFolder)
    {
        project = string.Empty;
        slice = string.Empty;
        typeFolder = string.Empty;

        if (parts.Length < 4 || parts[1] != "Features")
            return false;

        project = parts[0];
        slice = parts[2];
        typeFolder = parts[3];
        return true;
    }

}
