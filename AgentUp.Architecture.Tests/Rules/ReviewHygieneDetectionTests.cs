using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ReviewHygieneDetectionTests
{
    [TestCase("void Test() { Assume.That(!OperatingSystem.IsWindows()); }")]
    [TestCase("void Test() { if (OperatingSystem.IsWindows()) Assert.Ignore(\"Windows\"); }")]
    [TestCase("void Test() { if (!File.Exists(\"tool\")) Assert.Ignore(\"missing\"); }")]
    [TestCase("void Test() { try { Run(); } catch (DllNotFoundException ex) { Assert.Ignore(ex.Message); } }")]
    public void Runtime_coverage_skip_detection_matches_platform_and_system_state_guards(string method)
    {
        Assert.That(IsRuntimeCoverageSkip(method), Is.True);
    }

    [TestCase("void Test() { Assert.Ignore(\"Tracked external incident\"); }")]
    [TestCase("void Test() { Assume.That(featureEnabled); }")]
    [TestCase("void Test() { Assert.That(OperatingSystem.IsWindows(), Is.True); }")]
    public void Runtime_coverage_skip_detection_leaves_unrelated_test_control_alone(string method)
    {
        Assert.That(IsRuntimeCoverageSkip(method), Is.False);
    }

    private static bool IsRuntimeCoverageSkip(string method)
    {
        var root = CSharpSyntaxTree.ParseText($"class Fixture {{ {method} }}").GetRoot();
        var invocation = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(candidate => candidate.Expression.ToString() is "Assume.That" or "Assert.Ignore" or "Assert.That");
        return ReviewHygiene.IsRuntimeCoverageSkipInvocation(invocation);
    }
}
