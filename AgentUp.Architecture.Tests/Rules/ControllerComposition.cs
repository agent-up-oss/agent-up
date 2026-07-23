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
    public void Controllers_do_not_depend_on_providers_repositories_or_factories()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var lowLevelFolders = new[] { "Providers", "Repositories", "Factories" };
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Controllers"))
            .SelectMany(path => FindLowLevelControllerDependencies(root, path, lowLevelFolders))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers must route DTOs to domain services; providers, repositories, and factories stay behind services or composition roots.");
    }

    [Test]
    public void Controllers_stay_thin()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Controllers"))
            .SelectMany(path => FindComplexControllerMethods(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controller methods must stay at routing complexity: no loops/switch/try/catch, cyclomatic complexity <= 2, and <= 5 statements.");
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

    [Test]
    public void Feature_slices_do_not_import_sibling_slice_internals()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Select(path => (Path: path, Parts: ArchitectureFixture.Parts(root, path)))
            .Where(item => TryFeatureLocation(item.Parts, out _, out _, out var typeFolder)
                           && IsRuntimeBoundaryCaller(typeFolder))
            .SelectMany(item => FindCrossSliceInternalUsings(root, item.Path, item.Parts))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers, and Server domain services, must call sibling slices through Controllers/ or explicit contracts, not implementation services/providers/repositories/factories.");
    }

    private static bool ReturnTypeLooksInternal(TypeSyntax returnType)
    {
        var text = returnType.ToString();
        return InternalTypeFolders.Any(folder => text.Contains($".{folder}.", StringComparison.Ordinal))
               || InternalTypeFolders.Any(folder => ArchitectureFixture.FinalTypeSegment(returnType).EndsWith(folder[..^1], StringComparison.Ordinal));
    }

    private static IEnumerable<string> FindLowLevelControllerDependencies(string root, string path, IReadOnlyCollection<string> lowLevelFolders)
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

        var forbiddenSuffixes = new[] { "Provider", "Repository", "Factory" };
        foreach (var violation in rootNode.DescendantNodes().OfType<ParameterSyntax>()
                     .Where(parameter => parameter.Type is not null && TypeEndsWith(parameter.Type, forbiddenSuffixes))
                     .Select(parameter => $"{ArchitectureFixture.Location(root, path, tree, parameter)}: parameter {parameter.Type}")
                     .Concat(rootNode.DescendantNodes().OfType<FieldDeclarationSyntax>()
                         .Where(field => TypeEndsWith(field.Declaration.Type, forbiddenSuffixes))
                         .Select(field => $"{ArchitectureFixture.Location(root, path, tree, field)}: field {field.Declaration.Type}"))
                     .Concat(rootNode.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                         .Where(property => TypeEndsWith(property.Type, forbiddenSuffixes))
                         .Select(property => $"{ArchitectureFixture.Location(root, path, tree, property)}: property {property.Type}")))
            yield return violation;
    }

    private static IEnumerable<string> FindComplexControllerMethods(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => !method.Modifiers.Any(modifier => modifier.RawKind == (int)SyntaxKind.StaticKeyword))
            .SelectMany(method =>
            {
                var violations = new List<string>();
                var location = ArchitectureFixture.Location(root, path, tree, method);
                var statementCount = method.Body?.Statements.Count ?? (method.ExpressionBody is null ? 0 : 1);
                var complexity = CyclomaticComplexity(method);

                if (statementCount > 5)
                    violations.Add($"{location}: {method.Identifier.Text} has {statementCount} statements");
                if (complexity > 2)
                    violations.Add($"{location}: {method.Identifier.Text} has cyclomatic complexity {complexity}");
                if (method.DescendantNodes().Any(node => node is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax))
                    violations.Add($"{location}: {method.Identifier.Text} contains a loop");
                if (method.DescendantNodes().Any(node => node is SwitchStatementSyntax or SwitchExpressionSyntax))
                    violations.Add($"{location}: {method.Identifier.Text} contains a switch");
                if (method.DescendantNodes().Any(node => node is TryStatementSyntax or CatchClauseSyntax))
                    violations.Add($"{location}: {method.Identifier.Text} contains try/catch");

                return violations;
            });
    }

    private static int CyclomaticComplexity(MethodDeclarationSyntax method)
    {
        var complexity = 1;
        foreach (var node in method.DescendantNodes())
        {
            complexity += node switch
            {
                IfStatementSyntax => 1,
                ConditionalExpressionSyntax => 1,
                ForStatementSyntax => 1,
                ForEachStatementSyntax => 1,
                WhileStatementSyntax => 1,
                DoStatementSyntax => 1,
                CaseSwitchLabelSyntax => 1,
                SwitchExpressionArmSyntax => 1,
                BinaryExpressionSyntax binary when binary.RawKind is (int)SyntaxKind.LogicalAndExpression or (int)SyntaxKind.LogicalOrExpression => 1,
                _ => 0
            };
        }

        return complexity;
    }

    private static IEnumerable<(string Project, string Slice)> FindInboundFeatureTargets(string root, string path, string[] sourceParts)
    {
        var sourceIsFeature = TryFeatureLocation(sourceParts, out var sourceProject, out var sourceSlice, out _);
        if (!sourceIsFeature)
            yield break;

        var (_, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        var project = sourceIsFeature ? sourceProject : sourceParts[0];
        var prefix = $"{project}.Features.";

        foreach (var target in rootNode.DescendantNodes()
                     .OfType<UsingDirectiveSyntax>()
                     .Select(usingDirective => usingDirective.Name?.ToString())
                     .Where(name => name is not null && name.StartsWith(prefix, StringComparison.Ordinal))
                     .Select(name => name![prefix.Length..].Split('.'))
                     .Where(targetParts => targetParts.Length >= 1)
                     .Select(targetParts => targetParts[0])
                     .Where(targetSlice => !sourceIsFeature || targetSlice != sourceSlice))
            yield return (project, target);
    }

    private static bool FeatureHasController(string root, string project, string slice)
    {
        var controllers = Path.Join(root, project, "Features", slice, "Controllers");
        return Directory.Exists(controllers) &&
               Directory.EnumerateFiles(controllers, "*.cs", SearchOption.TopDirectoryOnly)
                   .Any(path =>
                   {
                       var (_, rootNode) = ArchitectureFixture.ParseSourceFile(path);
                       return rootNode.DescendantNodes()
                           .OfType<TypeDeclarationSyntax>()
                           .Any(type => type.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal)
                                        && !type.Modifiers.Any(modifier => modifier.RawKind == (int)SyntaxKind.StaticKeyword)
                                        && !type.Modifiers.Any(modifier => modifier.RawKind == (int)SyntaxKind.AbstractKeyword));
                   });
    }

    private static IEnumerable<string> FindCrossSliceInternalUsings(string root, string path, string[] sourceParts)
    {
        var sourceIsFeature = TryFeatureLocation(sourceParts, out var sourceProject, out var sourceSlice, out _);
        if (!sourceIsFeature && !IsApprovedEntrypoint(sourceParts))
            yield break;

        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        var project = sourceIsFeature ? sourceProject : sourceParts[0];
        var prefix = $"{project}.Features.";
        foreach (var violation in rootNode.DescendantNodes()
                     .OfType<UsingDirectiveSyntax>()
                     .Select(usingDirective => (Using: usingDirective, Name: usingDirective.Name?.ToString()))
                     .Where(item => item.Name is not null && item.Name.StartsWith(prefix, StringComparison.Ordinal))
                     .Select(item => (item.Using, TargetParts: item.Name![prefix.Length..].Split('.'), item.Name))
                     .Where(item => item.TargetParts.Length >= 2)
                     .Where(item => !sourceIsFeature || item.TargetParts[0] != sourceSlice)
                     .Where(item => !IsApprovedCrossSliceFolder(item.TargetParts[1]))
                     .Select(item => $"{ArchitectureFixture.Location(root, path, tree, item.Using)}: using {item.Name}"))
            yield return violation;
    }

    private static bool IsApprovedCrossSliceFolder(string folder)
        => folder is "Controllers" or "DTOs" or "Interfaces" or "Models" or "Tools";

    private static bool IsRuntimeBoundaryCaller(string typeFolder)
        => typeFolder is "Controllers" or "Services";

    private static bool TypeEndsWith(TypeSyntax type, IReadOnlyCollection<string> suffixes)
        => suffixes.Any(suffix => ArchitectureFixture.FinalTypeSegment(type).EndsWith(suffix, StringComparison.Ordinal));

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
