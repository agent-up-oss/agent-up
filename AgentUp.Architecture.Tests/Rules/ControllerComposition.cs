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

    private static bool ReturnTypeLooksInternal(TypeSyntax returnType)
    {
        var text = returnType.ToString();
        return InternalTypeFolders.Any(folder => text.Contains($".{folder}.", StringComparison.Ordinal))
               || InternalTypeFolders.Any(folder => ArchitectureFixture.FinalTypeSegment(returnType).EndsWith(folder[..^1], StringComparison.Ordinal));
    }
}
