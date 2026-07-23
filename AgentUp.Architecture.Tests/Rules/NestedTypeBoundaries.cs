using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis;
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
            .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            .Where(node => node.Parent is BaseTypeDeclarationSyntax)
            .Select(node => $"{ArchitectureFixture.Location(root, path, tree, node)}: nested {KindName(node)} {Identifier(node)}");
    }

    private static string KindName(SyntaxNode node)
        => node switch
        {
            ClassDeclarationSyntax => "class",
            RecordDeclarationSyntax => "record",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            EnumDeclarationSyntax => "enum",
            DelegateDeclarationSyntax => "delegate",
            _ => "type"
        };

    private static string Identifier(SyntaxNode node)
        => node switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.Text,
            DelegateDeclarationSyntax type => type.Identifier.Text,
            _ => ""
        };
}
