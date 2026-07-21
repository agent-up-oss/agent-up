using ArchUnitNET.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Fixtures;

internal static class ArchitectureFixture
{
    public static readonly string[] ProductionProjects =
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

    public static readonly string[] TestProjects =
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

    public static readonly string[] AllowedFeatureTypeFolders =
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

    public static readonly string[] AllowedSharedTypeFolders =
    [
        "Factories",
        "Interfaces",
        "Providers"
    ];

    public static readonly string[] AllowedTestKindFolders =
    [
        "Architecture",
        "Automation",
        "Commands",
        "E2E",
        "Fake",
        "Headless",
        "HTTP",
        "Http",
        "Provider",
        "Repository",
        "Resources",
        "Support",
        "TerminalIntegration",
        "Tools",
        "Unit"
    ];

    public static readonly string[] ForbiddenUnitTestTokens =
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
        "Socket",
        "new FileInfo(",
        "new DirectoryInfo(",
        "new FileStream("
    ];

    public static readonly string[] ForbiddenInstallerAppServiceTokens =
    [
        "Path.GetTempPath(",
        "Environment.GetFolderPath(",
        "Environment.GetEnvironmentVariable("
    ];


    public static readonly ArchUnitNET.Domain.Architecture ArchUnitArchitecture = new ArchLoader()
        .LoadAssemblies(ProductionProjects.Select(System.Reflection.Assembly.Load).ToArray())
        .Build();

    public static string FindRepositoryRoot(string startDirectory)
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

    public static IEnumerable<string> ProductionSourceFiles(string repositoryRoot)
        => ProductionProjects.SelectMany(project => ProjectSourceFiles(repositoryRoot, project));

    public static IEnumerable<string> TestSourceFiles(string repositoryRoot)
        => TestProjects
            .Where(project => Directory.Exists(Path.Join(repositoryRoot, project)))
            .SelectMany(project => ProjectSourceFiles(repositoryRoot, project));

    public static IEnumerable<string> AllSourceFiles(string repositoryRoot)
        => ProductionSourceFiles(repositoryRoot).Concat(TestSourceFiles(repositoryRoot));

    public static IEnumerable<string> ProjectSourceFiles(string repositoryRoot, string project)
        => Directory.EnumerateFiles(Path.Join(repositoryRoot, project), "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasPathPart(repositoryRoot, path, "bin") && !HasPathPart(repositoryRoot, path, "obj"));

    public static IEnumerable<string> FindConstructionsInFile(string root, string path, string[] typeSuffixes)
    {
        var (tree, rootNode) = ParseSourceFile(path);

        var objectCreations = rootNode.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(node =>
            {
                var typeName = FinalTypeSegment(node.Type);
                return typeSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));
            });

        foreach (var creation in objectCreations)
            yield return $"{Location(root, path, tree, creation)}: new {creation.Type}(...)";
    }

    public static IEnumerable<string> FindStaticFactoryMethodsInFile(string root, string path)
    {
        var (tree, rootNode) = ParseSourceFile(path);

        var staticMethods = rootNode.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method =>
                method.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                method.Identifier.Text.StartsWith("Create", StringComparison.Ordinal));

        foreach (var method in staticMethods)
        {
            var modifiers = string.Join(" ", method.Modifiers.Select(m => m.Text));
            var returnType = method.ReturnType.ToString();
            var methodName = method.Identifier.Text;
            yield return $"{Location(root, path, tree, method)}: {modifiers} {returnType} {methodName}(...)";
        }
    }

    public static (SyntaxTree Tree, CompilationUnitSyntax Root) ParseSourceFile(string path)
    {
        var source = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree, tree.GetCompilationUnitRoot());
    }

    public static string Location(string root, string path, SyntaxTree tree, SyntaxNode node)
    {
        var lineSpan = tree.GetLineSpan(node.Span);
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        return $"{Relative(root, path)}:{lineNumber}";
    }

    public static string FinalTypeSegment(TypeSyntax type)
    {
        var value = type switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            NullableTypeSyntax nullable => FinalTypeSegment(nullable.ElementType),
            ArrayTypeSyntax array => FinalTypeSegment(array.ElementType),
            _ => type.ToString()
        };

        var genericDelimiter = value.IndexOf('<', StringComparison.Ordinal);
        if (genericDelimiter >= 0)
            value = value[..genericDelimiter];

        return value.Split('.').Last();
    }

    public static bool HasPathPart(string root, string path, string part)
        => Parts(root, path).Contains(part, StringComparer.Ordinal);

    public static string[] Parts(string root, string path)
        => Relative(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string Relative(string root, string path)
        => Path.GetRelativePath(root, path);
}
