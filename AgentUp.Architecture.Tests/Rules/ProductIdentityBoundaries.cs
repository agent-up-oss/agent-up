using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ProductIdentityBoundaries
{
    [Test]
    public void Packaging_does_not_depend_on_installer_product_session_models()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProjectSourceFiles(root, "AgentUp.Packaging")
            .SelectMany(path => UsingDirectives(root, path)
                .Where(item => item.Name.StartsWith("AgentUp.Installers.Features.Installation.", StringComparison.Ordinal)
                               || item.Name.Equals("AgentUp.Installers.Features.Installation", StringComparison.Ordinal))
                .Select(item => $"{item.Location}: using {item.Name}"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Packaging must expose package-owned DTOs and map them to installer/platform contracts at the packaging boundary; do not depend on installer workflow product/session internals.");
    }

    [Test]
    public void Package_product_slug_path_components_are_validated_at_request_boundaries()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProjectSourceFiles(root, "AgentUp.Packaging")
            .SelectMany(path => Constructors(root, path)
                .Where(item => item.Constructor.ParameterList.Parameters.Any(parameter =>
                    parameter.Identifier.Text.Contains("product", StringComparison.OrdinalIgnoreCase)))
                .Where(item => item.Constructor.Body is not null)
                .Where(item => !item.Constructor.Body!.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(IsProductSlugValidation))
                .Select(item => $"{item.Location}: product manifest constructor without product slug validation"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Any package request or DTO boundary that accepts product identity must validate the product slug before layouts use it in artifact names or paths.");
    }

    [Test]
    public void Windows_cli_shim_paths_validate_manifest_filenames_before_joining()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = new[] { "AgentUp.Installers", "AgentUp.Packaging" }
            .SelectMany(project => ArchitectureFixture.ProjectSourceFiles(root, project))
            .SelectMany(path => Invocations(root, path)
                .Where(item => IsPathJoinOrWindowsCombine(item.Invocation))
                .Where(item => item.Invocation.ArgumentList.Arguments.Any(argument =>
                    IsShimNameExpression(argument.Expression)))
                .Where(item => !item.Invocation.ArgumentList.Arguments.Any(argument =>
                    IsSafeShimNameValidation(argument.Expression)))
                .Select(item => $"{item.Location}: shim filename used in path without RequireSafeCliShimFileName(...)"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "CLI shim names must be validated as safe filenames before path construction so separators or dot segments cannot escape the bin directory.");
    }

    [Test]
    public void Windows_wix_guid_seeds_are_scoped_to_product_identity()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProjectSourceFiles(root, "AgentUp.Installers")
            .SelectMany(path => Methods(root, path)
                .Where(item => item.Method.Identifier.Text.Contains("Guid", StringComparison.Ordinal))
                .Where(item => item.Method.Body is not null)
                .Where(item => item.Method.Body!.DescendantNodes()
                    .OfType<LiteralExpressionSyntax>()
                    .Any(literal => IsInstallerGuidSeedLiteral(literal.Token.ValueText)))
                .Where(item => !MethodText(item.Method).Contains("UpgradeCode", StringComparison.Ordinal)
                               || !MethodText(item.Method).Contains("UsesLegacy", StringComparison.Ordinal))
                .Select(item => $"{item.Location}: installer GUID seed is not product-scoped with a documented legacy branch"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Installer GUID seeds for fixed components, shortcuts, and bundles must include product identity so different products cannot collide; unscoped seeds require an explicit legacy compatibility branch.");
    }

    private static IEnumerable<(string Name, string Location)> UsingDirectives(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(usingDirective => (Name: usingDirective.Name?.ToFullString().Trim() ?? "", Location: ArchitectureFixture.Location(root, path, tree, usingDirective)));
    }

    private static IEnumerable<(ConstructorDeclarationSyntax Constructor, string Location)> Constructors(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Select(constructor => (constructor, ArchitectureFixture.Location(root, path, tree, constructor)));
    }

    private static IEnumerable<(MethodDeclarationSyntax Method, string Location)> Methods(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(method => (method, ArchitectureFixture.Location(root, path, tree, method)));
    }

    private static IEnumerable<(InvocationExpressionSyntax Invocation, string Location)> Invocations(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => (invocation, ArchitectureFixture.Location(root, path, tree, invocation)));
    }

    private static bool IsPathJoinOrWindowsCombine(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text == "Join"
                                                         && memberAccess.Expression is IdentifierNameSyntax { Identifier.Text: "Path" },
            IdentifierNameSyntax identifier => identifier.Identifier.Text == "WindowsCombine",
            _ => false
        };

    private static bool IsProductSlugValidation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "RequireSafePathComponent" }
           && invocation.ArgumentList.Arguments.Any(argument => argument.Expression.ToFullString().Contains(".Slug", StringComparison.Ordinal));

    private static bool IsShimNameExpression(ExpressionSyntax expression)
    {
        var text = expression.ToFullString();
        return text.Contains("ShimName", StringComparison.Ordinal)
               || text.Contains("shimName", StringComparison.Ordinal);
    }

    private static bool IsSafeShimNameValidation(ExpressionSyntax expression)
        => expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "RequireSafeCliShimFileName" });

    private static bool IsInstallerGuidSeedLiteral(string value)
        => value is "bundle-upgrade" or "cli-shim" or "start-menu-shortcut" or "installer-start-menu-shortcut";

    private static string MethodText(MethodDeclarationSyntax method)
        => method.ToFullString();
}
