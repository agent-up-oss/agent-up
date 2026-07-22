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
            .SelectMany(path => ForbiddenInstallerWorkflowReferences(root, path))
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
                .Where(item => item.Constructor.ParameterList.Parameters.Any(parameter => IsProductIdentityParameter(parameter.Identifier.Text)))
                .Where(item => item.Constructor.Body is not null)
                .SelectMany(item => ProductSlugValidationOrderViolations(item.Constructor, item.Location)))
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
            .SelectMany(path => ShimPathValidationViolations(root, path))
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
                .Where(item => UsesFixedInstallerGuidSeeds(item.Method))
                .Where(item => !HasProductScopedGuidSeedWithLegacyBranch(item.Method))
                .Select(item => $"{item.Location}: installer GUID seed is not product-scoped with a documented legacy branch"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Installer GUID seeds for fixed components, shortcuts, and bundles must include product identity so different products cannot collide; unscoped seeds require an explicit legacy compatibility branch.");
    }

    private static IEnumerable<string> ForbiddenInstallerWorkflowReferences(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        const string forbiddenNamespace = "AgentUp.Installers.Features.Installation";

        foreach (var usingDirective in rootNode.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var name = usingDirective.Name?.ToFullString().Trim() ?? "";
            if (name.Equals(forbiddenNamespace, StringComparison.Ordinal)
                || name.StartsWith(forbiddenNamespace + ".", StringComparison.Ordinal))
                yield return $"{ArchitectureFixture.Location(root, path, tree, usingDirective)}: using {name}";
        }

        foreach (var qualifiedName in rootNode.DescendantNodes().OfType<QualifiedNameSyntax>())
        {
            var name = qualifiedName.ToFullString().Trim();
            if (name.Equals(forbiddenNamespace, StringComparison.Ordinal)
                || name.StartsWith(forbiddenNamespace + ".", StringComparison.Ordinal))
                yield return $"{ArchitectureFixture.Location(root, path, tree, qualifiedName)}: {name}";
        }

        foreach (var memberAccess in rootNode.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var name = memberAccess.ToFullString().Trim();
            if (name.Equals(forbiddenNamespace, StringComparison.Ordinal)
                || name.StartsWith(forbiddenNamespace + ".", StringComparison.Ordinal))
                yield return $"{ArchitectureFixture.Location(root, path, tree, memberAccess)}: {name}";
        }
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

    private static IEnumerable<string> ProductSlugValidationOrderViolations(ConstructorDeclarationSyntax constructor, string location)
    {
        var body = constructor.Body;
        if (body is null)
            yield break;

        if (ContainingTypeName(constructor) == "PackageProductManifest")
        {
            if (!body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsProductSlugDirectValidation))
                yield return $"{location}: package product manifest constructor without direct slug validation";
            yield break;
        }

        var validationSeen = false;
        foreach (var statement in body.Statements)
        {
            if (statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsProductManifestValidation))
                validationSeen = true;

            if (!validationSeen && statement.DescendantNodesAndSelf().Any(IsProductSlugPathOrFilenameUse))
                yield return $"{location}: product slug can be used before PackageProductManifest.Validate(...)";
        }

        if (!validationSeen)
            yield return $"{location}: product manifest constructor without PackageProductManifest.Validate(...)";
    }

    private static bool IsProductIdentityParameter(string parameterName)
        => parameterName.Contains("product", StringComparison.OrdinalIgnoreCase)
           || parameterName.Equals("slug", StringComparison.OrdinalIgnoreCase);

    private static string? ContainingTypeName(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;

    private static IEnumerable<string> ShimPathValidationViolations(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);

        foreach (var scope in rootNode.DescendantNodes().Where(node =>
                     node is MethodDeclarationSyntax or ConstructorDeclarationSyntax or AccessorDeclarationSyntax or PropertyDeclarationSyntax))
        {
            var validatedLocals = ValidatedShimLocalNames(scope).ToHashSet(StringComparer.Ordinal);
            foreach (var invocation in scope.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(IsPathJoinOrWindowsCombine))
            {
                var unsafeArguments = invocation.ArgumentList.Arguments
                    .Select(argument => argument.Expression)
                    .Where(IsShimNameExpression)
                    .Where(expression => !IsSafeShimNameValidation(expression))
                    .Where(expression => expression is not IdentifierNameSyntax identifier || !validatedLocals.Contains(identifier.Identifier.Text))
                    .ToArray();

                if (unsafeArguments.Length > 0)
                    yield return $"{ArchitectureFixture.Location(root, path, tree, invocation)}: shim filename used in path without RequireSafeCliShimFileName(...)";
            }
        }
    }

    private static bool IsPathJoinOrWindowsCombine(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text == "Join"
                                                         && memberAccess.Expression is IdentifierNameSyntax { Identifier.Text: "Path" }
                                                            or QualifiedNameSyntax { Right.Identifier.Text: "Path" }
                                                            or MemberAccessExpressionSyntax { Name.Identifier.Text: "Path" },
            IdentifierNameSyntax identifier => identifier.Identifier.Text == "WindowsCombine",
            _ => false
        };

    private static bool IsProductManifestValidation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Validate" } memberAccess
           && memberAccess.Expression is IdentifierNameSyntax { Identifier.Text: "PackageProductManifest" };

    private static bool IsProductSlugDirectValidation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "RequireSafePathComponent" }
           && invocation.ArgumentList.Arguments.Any(argument =>
               argument.Expression is IdentifierNameSyntax { Identifier.Text: "slug" });

    private static bool IsProductSlugPathOrFilenameUse(SyntaxNode node)
        => node is InvocationExpressionSyntax invocation
           && IsPathJoinOrWindowsCombine(invocation)
           && invocation.ArgumentList.Arguments.Any(argument => IsProductSlugExpression(argument.Expression));

    private static bool IsProductSlugExpression(ExpressionSyntax expression)
        => expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => memberAccess.Name.Identifier.Text == "Slug");

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

    private static IEnumerable<string> ValidatedShimLocalNames(SyntaxNode scope)
        => scope.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(variable => variable.Initializer is not null && IsSafeShimNameValidation(variable.Initializer.Value))
            .Select(variable => variable.Identifier.Text);

    private static bool UsesFixedInstallerGuidSeeds(MethodDeclarationSyntax method)
        => method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsGuidHashInvocation)
           && ContainsFixedInstallerGuidSeed(method);

    private static bool HasProductScopedGuidSeedWithLegacyBranch(MethodDeclarationSyntax method)
        => method.DescendantNodes()
            .OfType<ConditionalExpressionSyntax>()
            .Any(conditional => conditional.Condition.DescendantNodesAndSelf()
                                    .OfType<MemberAccessExpressionSyntax>()
                                    .Any(memberAccess => memberAccess.Name.Identifier.Text.Contains("Legacy", StringComparison.Ordinal))
                                && ContainsFixedInstallerGuidSeed(conditional.WhenTrue)
                                && ContainsManifestUpgradeCode(conditional.WhenFalse));

    private static bool IsGuidHashInvocation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "GetBytes" or "HashData" };

    private static bool ContainsFixedInstallerGuidSeed(SyntaxNode node)
        => node.DescendantNodesAndSelf()
            .OfType<LiteralExpressionSyntax>()
            .Any(literal => literal.Token.ValueText is "Agent-Up Windows Installer:"
                or "bundle-upgrade"
                or "cli-shim"
                or "start-menu-shortcut"
                or "installer-start-menu-shortcut");

    private static bool ContainsManifestUpgradeCode(SyntaxNode node)
        => node.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => memberAccess.Name.Identifier.Text == "UpgradeCode");
}
