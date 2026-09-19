using System.Text.RegularExpressions;
using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Rules holding test data construction behind builders instead of production constructors.
/// </summary>
/// <remarks>
/// A DTO or model built positionally at a hundred call sites turns every signature change
/// into a shotgun edit, and tells a reader nothing about which workspace, application or
/// commit a given test means. The two rules here work as a pair: the first names the types
/// that have grown into coupling hotspots and requires a builder for them, and the second
/// makes the builder the only way in once it exists, so a migrated type cannot drift back
/// below the threshold and quietly re-acquire inline call sites.
/// <para>
/// The type set is deliberately narrow: types declared under <c>Features/*/DTOs</c> or
/// <c>Features/*/Models</c> of the production project a test project is paired with, with
/// more than <see cref="MaximumPositionalParameters"/> constructor parameters. A two-field
/// record reads perfectly well positionally and does not need a builder; a nine-field one
/// with six optional fields does not read at all.
/// </para>
/// </remarks>
[TestFixture]
public sealed class TestDataBuilders
{
    /// <summary>
    /// Constructions of one type in one test project before a builder is required. Fifteen
    /// is well above the handful a focused fixture needs and well below the counts that
    /// made this a problem.
    /// </summary>
    private const int MaximumInlineConstructions = 15;

    /// <summary>
    /// A constructor this size or smaller still reads at a call site, so it is left alone.
    /// </summary>
    private const int MaximumPositionalParameters = 3;

    private const string SupportFolder = "Support";

    [Test]
    public void Types_built_many_times_in_one_test_project_have_a_builder()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);

        var violations = ArchitectureFixture.TestProjects
            .SelectMany(project => HotspotTypes(root, project)
                .Where(hotspot => !BuilderExists(root, project, hotspot.Type))
                .Select(hotspot =>
                    $"{project} builds {hotspot.Type} inline {hotspot.Count} times: add "
                    + $"{project}/{SupportFolder}/{hotspot.Type}Builder.cs"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"A production DTO or model constructed more than {MaximumInlineConstructions} times in one "
            + "test project must be built through a builder in that project's Support/ folder, so a "
            + "signature change edits one file instead of every call site.");
    }

    [Test]
    public void Tests_do_not_construct_a_type_that_already_has_a_builder()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);

        var violations = ArchitectureFixture.TestProjects
            .SelectMany(project => BuiltTypes(root, project)
                .SelectMany(type => ArchitectureFixture.ProjectSourceFiles(root, project)
                    .Where(path => !ArchitectureFixture.HasPathPart(root, path, SupportFolder))
                    .SelectMany(path => ConstructionSites(root, path, type))))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            "A type with a Support/ builder must be built through it. Constructing it inline puts the "
            + "call site back on the production constructor the builder exists to absorb.");
    }

    /// <summary>
    /// Types of the production projects this test project covers, declared under a feature's
    /// DTOs or Models folder, whose constructor is too wide to read positionally.
    /// </summary>
    private static HashSet<string> CandidateTypes(string root, string testProject)
        => CoveredProjects(root, testProject)
            .SelectMany(production => ArchitectureFixture.ProjectSourceFiles(root, production))
            .Where(path => IsDataFolder(root, path))
            .SelectMany(WideTypeNames)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The production projects a test project covers: its same-named sibling where one
    /// exists, and otherwise the production projects it references.
    /// </summary>
    /// <remarks>
    /// The sibling is the normal case and the one AGENTS.md names - tests belong with the
    /// project that owns the type. <c>AgentUp.Tests</c> is the exception: it is a
    /// cross-product suite driving the Server and the Desktop against each other, with no
    /// production project of its own, and reading its references is what keeps it inside
    /// this rule rather than exempt from it by an accident of naming.
    /// </remarks>
    private static IEnumerable<string> CoveredProjects(string root, string testProject)
    {
        var sibling = testProject[..^".Tests".Length];
        if (Directory.Exists(Path.Join(root, sibling)))
            return [sibling];

        var project = Path.Join(root, testProject, $"{testProject}.csproj");
        if (!File.Exists(project))
            return [];

        return ReferencedProjects(project)
            .Where(referenced => ArchitectureFixture.ProductionProjects.Contains(referenced, StringComparer.Ordinal));
    }

    private static IEnumerable<string> ReferencedProjects(string projectFile)
        => ProjectReference
            .Matches(File.ReadAllText(projectFile))
            .Select(match => match.Groups["name"].Value);

    /// <summary>A ProjectReference's target project name, taken from its csproj file name.</summary>
    private static readonly Regex ProjectReference =
        new(@"<ProjectReference\s+Include=""[^""]*?(?<name>[^""\\/]+)\.csproj""", RegexOptions.Compiled);

    private static bool IsDataFolder(string root, string path)
    {
        var parts = ArchitectureFixture.Parts(root, path);
        var folder = parts.Length >= 2 ? parts[^2] : string.Empty;
        return (folder is "DTOs" or "Models") && parts.Contains("Features", StringComparer.Ordinal);
    }

    private static IEnumerable<string> WideTypeNames(string path)
    {
        var (_, node) = ArchitectureFixture.ParseSourceFile(path);

        return node.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(IsWide)
            .Select(declaration => declaration.Identifier.Text);
    }

    private static bool IsWide(TypeDeclarationSyntax declaration)
        => declaration.ParameterList?.Parameters.Count > MaximumPositionalParameters
           || declaration.Members.OfType<ConstructorDeclarationSyntax>()
               .Any(constructor => constructor.ParameterList.Parameters.Count > MaximumPositionalParameters);

    private static IEnumerable<(string Type, int Count)> HotspotTypes(string root, string project)
    {
        var candidates = CandidateTypes(root, project);
        if (candidates.Count == 0)
            return [];

        return ArchitectureFixture.ProjectSourceFiles(root, project)
            .SelectMany(ConstructedTypeNames)
            .Where(candidates.Contains)
            .GroupBy(type => type, StringComparer.Ordinal)
            .Where(group => group.Count() > MaximumInlineConstructions)
            .Select(group => (group.Key, group.Count()));
    }

    /// <summary>Types the test project already has a builder for, by file name.</summary>
    private static HashSet<string> BuiltTypes(string root, string project)
    {
        var support = Path.Join(root, project, SupportFolder);
        if (!Directory.Exists(support))
            return [];

        return Directory.EnumerateFiles(support, "*Builder.cs")
            .Select(path => Path.GetFileNameWithoutExtension(path)[..^"Builder".Length])
            .Where(type => CandidateTypes(root, project).Contains(type))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool BuilderExists(string root, string project, string type)
        => File.Exists(Path.Join(root, project, SupportFolder, $"{type}Builder.cs"));

    private static IEnumerable<string> ConstructedTypeNames(string path)
    {
        var (_, node) = ArchitectureFixture.ParseSourceFile(path);
        return node.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Select(creation => ArchitectureFixture.FinalTypeSegment(creation.Type));
    }

    private static IEnumerable<string> ConstructionSites(string root, string path, string type)
    {
        var (tree, node) = ArchitectureFixture.ParseSourceFile(path);

        var named = node.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => ArchitectureFixture.FinalTypeSegment(creation.Type) == type);

        // `new(...)` carries no type of its own, so it is judged by the type it is written
        // into - the variable, field, property or return type it initialises. Without that
        // the builder rule would have a loophole that reads exactly like the construction
        // it bans.
        var implicitly = node.DescendantNodes()
            .OfType<ImplicitObjectCreationExpressionSyntax>()
            .Where(creation => TargetTypeName(creation) == type);

        return named.Cast<SyntaxNode>()
            .Concat(implicitly)
            .Select(creation => Violation(root, path, tree, creation, type));
    }

    private static string Violation(string root, string path, SyntaxTree tree, SyntaxNode creation, string type)
        => $"{ArchitectureFixture.Location(root, path, tree, creation)}: {type} is built inline; "
           + $"use {type}Builder";

    /// <summary>
    /// The nearest enclosing declaration that states a type, with one level of generic
    /// element unwrapping so an item inside a <c>List&lt;T&gt;</c> is judged as a T.
    /// </summary>
    private static string? TargetTypeName(SyntaxNode creation)
    {
        for (var node = creation.Parent; node is not null; node = node.Parent)
        {
            TypeSyntax? declared = node switch
            {
                VariableDeclarationSyntax variable => variable.Type,
                PropertyDeclarationSyntax property => property.Type,
                MethodDeclarationSyntax method => method.ReturnType,
                ParameterSyntax parameter => parameter.Type,
                _ => null
            };

            if (declared is null)
                continue;

            return declared is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count == 1
                ? ArchitectureFixture.FinalTypeSegment(generic.TypeArgumentList.Arguments[0])
                : ArchitectureFixture.FinalTypeSegment(declared);
        }

        return null;
    }
}
