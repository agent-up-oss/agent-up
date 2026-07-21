using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class InstallerAppServiceComposition
{
    [Test]
    public void InstallerApp_services_do_not_contain_environment_lookup_or_temp_path_construction()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProjectSourceFiles(root, "AgentUp.InstallerApp")
            .Where(path => ArchitectureFixture.HasPathPart(root, path, "Services"))
            .SelectMany(path => ArchitectureFixture.ForbiddenInstallerAppServiceTokens
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{ArchitectureFixture.Relative(root, path)} contains {token}"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "InstallerApp service classes must not perform environment lookup or construct temp paths; move composition to Factories.");
    }
}
