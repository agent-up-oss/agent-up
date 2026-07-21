using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class InstallerAppViewModelComposition
{
    [Test]
    public void InstallerApp_view_models_do_not_contain_static_factory_methods()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProjectSourceFiles(root, "AgentUp.InstallerApp")
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "ViewModels"))
            .SelectMany(path => ArchitectureFixture.MatchingLines(root, path, ArchitectureFixture.StaticFactoryInViewModel))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "InstallerApp view models must not contain static factory methods; move composition to Factories.");
    }
}
