using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ControllerComposition
{
    [Test]
    public void Controllers_do_not_construct_services_providers_repositories_or_factories()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Controllers"))
            .SelectMany(path => ArchitectureFixture.MatchingLines(root, path, ArchitectureFixture.ConstructionInController))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Controllers must receive dependencies through constructors and map DTO calls to services.");
    }
}
