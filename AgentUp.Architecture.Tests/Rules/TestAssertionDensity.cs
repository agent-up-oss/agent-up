using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// A cap on how much one test method may assert.
/// </summary>
/// <remarks>
/// Roughly three quarters of this suite's tests carry one to three assertions, which is
/// healthy; the problem is a thin tail of methods asserting twenty or thirty things at
/// once. Such a test names one behaviour and covers several, so a failure does not say what
/// broke and the method can only grow. The cap is deliberately far above the healthy range:
/// it is a ceiling that catches the tail, not a target.
/// <para>
/// Assume.That is counted too. A test that decides at runtime whether it applies reports
/// success without having run, so it is not an escape from the cap - and platform and CI
/// branches belong behind an injected capability provider rather than a skip.
/// </para>
/// </remarks>
[TestFixture]
public sealed class TestAssertionDensity
{
    /// <summary>
    /// Assertions one test method may make. Above this it is covering more than one
    /// behaviour and should be split into tests that each name theirs.
    /// </summary>
    private const int MaximumAssertionsPerTest = 10;

    private static readonly string[] AssertionEntryPoints = ["Assert", "Assume", "ClassicAssert", "StringAssert", "CollectionAssert"];

    [Test]
    public void No_test_method_asserts_more_than_the_cap()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);

        var violations = ArchitectureFixture.TestSourceFiles(root)
            .SelectMany(path => OverspecifiedMethods(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"A test method must make at most {MaximumAssertionsPerTest} assertions. Split it into tests "
            + "that each name one behaviour, so a failure says which one broke.");
    }

    private static IEnumerable<string> OverspecifiedMethods(string root, string path)
    {
        var (tree, node) = ArchitectureFixture.ParseSourceFile(path);

        foreach (var method in node.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
            if (body is null)
                continue;

            var assertions = body.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Count(IsAssertion);

            if (assertions > MaximumAssertionsPerTest)
                yield return $"{ArchitectureFixture.Location(root, path, tree, method)}: "
                           + $"{method.Identifier.Text} asserts {assertions} times";
        }
    }

    /// <summary>
    /// A call on one of the assertion entry points. Assert.Multiple is not counted itself -
    /// it is a grouping construct, and the assertions inside it are counted individually.
    /// </summary>
    private static bool IsAssertion(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
            return false;

        if (member.Name.Identifier.Text == "Multiple")
            return false;

        return AssertionEntryPoints.Contains(
            ArchitectureFixture.FinalTypeSegment(member.Expression as TypeSyntax
                ?? SyntaxFactoryTypeOf(member.Expression)),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The receiver of a member access as a type name, for the identifier form that the
    /// parser does not hand back as a <see cref="TypeSyntax"/>.
    /// </summary>
    private static TypeSyntax SyntaxFactoryTypeOf(ExpressionSyntax expression)
        => expression as TypeSyntax
           ?? Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseTypeName(expression.ToString());
}
