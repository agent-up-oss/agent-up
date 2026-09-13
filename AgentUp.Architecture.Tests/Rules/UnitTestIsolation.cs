using System.Text.RegularExpressions;
using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class UnitTestIsolation
{
    [Test]
    public void Unit_tests_do_not_use_real_io_process_socket_or_environment_mutation()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.TestSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Unit"))
            .Select(path => (Path: path, Body: Body(path)))
            .SelectMany(file => ArchitectureFixture.ForbiddenUnitTestTokens
                .Where(file.Body.Contains)
                .Select(token => $"{ArchitectureFixture.Relative(root, file.Path)} contains {token}"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Tests that use real filesystem, process, socket, current-directory, or environment mutation APIs must not live in Unit folders.");
    }

    /// <summary>
    /// A namespace or using directive, including its global and alias forms: a dotted name,
    /// optionally aliased to another, and nothing else. Matched precisely so a
    /// "using var stream = new FileStream(...)" statement, which is exactly what this rule
    /// exists to catch, is never mistaken for a directive - and completely, so a
    /// "global using System.Net.Sockets;" is not scanned and reported as a violation it is
    /// not.
    /// </summary>
    private static readonly Regex Declaration =
        new(@"^\s*(namespace|(global\s+)?using)\s+(static\s+)?([\w.]+\s*=\s*)?[\w.]+\s*[;{]\s*$",
            RegexOptions.Compiled);

    /// <summary>
    /// The file without its namespace and using directives. The tokens are matched as
    /// substrings and a slice or type name can end in one of them - a namespace segment
    /// "DotEnvFile" reads as "File." once the dot that follows it is counted - so the
    /// declarations, where no API can be called, are not searched.
    /// </summary>
    private static string Body(string path)
        => string.Join(
            Environment.NewLine,
            File.ReadLines(path).Where(line => !Declaration.IsMatch(line)));
}
