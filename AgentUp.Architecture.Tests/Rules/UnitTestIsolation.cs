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
            .SelectMany(path => ArchitectureFixture.ForbiddenUnitTestTokens
                .Where(File.ReadAllText(path).Contains)
                .Select(token => $"{ArchitectureFixture.Relative(root, path)} contains {token}"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Tests that use real filesystem, process, socket, current-directory, or environment mutation APIs must not live in Unit folders.");
    }
}
