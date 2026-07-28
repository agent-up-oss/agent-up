using AgentUp.Architecture.Tests.Fixtures;
using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ProjectDependencies
{
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
        AssertDoesNotDependOn("AgentUp.InstallerApp", Except("AgentUp.InstallerApp", "AgentUp.Installers"));
        AssertDoesNotDependOn("AgentUp.Packaging", Except("AgentUp.Packaging", "AgentUp.Installers"));
        AssertDoesNotDependOn("AgentUp.PackageSmoke", Except("AgentUp.PackageSmoke", "AgentUp.Installers"));
    }

    [Test]
    public void InstallerApp_nonAgentUpTypes_do_not_reference_capabilities_namespace()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProjectSourceFiles(root, "AgentUp.InstallerApp")
            .SelectMany(path => InstallerAppCapabilityReferences(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "InstallerApp product-generic types must use InstallerApp-owned capability catalog contracts instead of compile-time references to AgentUp.Capabilities.");
    }

    private static void AssertDoesNotDependOn(string sourceAssembly, IReadOnlyCollection<string> allowedAssemblies)
    {
        var source = Types().That().ResideInAssembly(sourceAssembly).As(sourceAssembly);

        foreach (var forbiddenAssembly in ArchitectureFixture.ProductionProjects.Except(allowedAssemblies))
        {
            var forbidden = Types().That().ResideInAssembly(forbiddenAssembly).As(forbiddenAssembly);
            IArchRule rule = Types().That().Are(source).Should().NotDependOnAny(forbidden)
                .Because($"{sourceAssembly} must not take runtime dependencies on {forbiddenAssembly}")
                .WithoutRequiringPositiveResults();
            rule.Check(ArchitectureFixture.ArchUnitArchitecture);
        }
    }

    private static string[] Except(params string[] allowed) => allowed;

    private static IEnumerable<string> InstallerAppCapabilityReferences(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        if (rootNode.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Any(type => type.Identifier.Text.StartsWith("AgentUp", StringComparison.Ordinal)))
            yield break;

        foreach (var usingDirective in rootNode.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var name = usingDirective.Name?.ToFullString().Trim() ?? "";
            if (IsCapabilitiesNamespace(name))
                yield return $"{ArchitectureFixture.Location(root, path, tree, usingDirective)}: using {name}";
        }

        foreach (var qualifiedName in rootNode.DescendantNodes().OfType<QualifiedNameSyntax>())
        {
            var name = qualifiedName.ToFullString().Trim();
            if (IsCapabilitiesNamespace(name))
                yield return $"{ArchitectureFixture.Location(root, path, tree, qualifiedName)}: {name}";
        }
    }

    private static bool IsCapabilitiesNamespace(string name) =>
        name.Equals("AgentUp.Capabilities", StringComparison.Ordinal)
        || name.StartsWith("AgentUp.Capabilities.", StringComparison.Ordinal);
}
