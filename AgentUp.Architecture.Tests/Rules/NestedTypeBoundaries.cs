using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class NestedTypeBoundaries
{
    [Test]
    public void Production_source_does_not_use_nested_types()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .SelectMany(path => FindNestedTypes(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Nested production types hide responsibilities and test seams. Move them to named feature or shared type folders.");
    }

    private static IEnumerable<string> FindNestedTypes(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(type => type.Parent is BaseTypeDeclarationSyntax)
            .Select(type => $"{ArchitectureFixture.Location(root, path, tree, type)}: nested {KindName(type)} {type.Identifier.Text}");
    }

    private static string KindName(BaseTypeDeclarationSyntax type)
        => type switch
        {
            ClassDeclarationSyntax => "class",
            RecordDeclarationSyntax => "record",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            EnumDeclarationSyntax => "enum",
            _ => "type"
        };
}
