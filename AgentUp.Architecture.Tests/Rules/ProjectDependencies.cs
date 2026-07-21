using AgentUp.Architecture.Tests.Fixtures;
using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ProjectDependencies
{
    [Test]
    public void Production_project_dependencies_follow_ownership_boundaries()
    {
        AssertDoesNotDependOn("AgentUp.Capabilities.Abstractions", Except("AgentUp.Capabilities.Abstractions"));
        AssertDoesNotDependOn("AgentUp.Capabilities.Common", Except("AgentUp.Capabilities.Common", "AgentUp.Capabilities.Abstractions"));
        AssertDoesNotDependOn("AgentUp.Capabilities.Dotnet", Except("AgentUp.Capabilities.Dotnet", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Common"));
        AssertDoesNotDependOn("AgentUp.Capabilities.Docker", Except("AgentUp.Capabilities.Docker", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Common"));
        AssertDoesNotDependOn("AgentUp.Server", Except("AgentUp.Server", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Dotnet", "AgentUp.Capabilities.Docker"));
        AssertDoesNotDependOn("AgentUp.Desktop", Except("AgentUp.Desktop"));
        AssertDoesNotDependOn("AgentUp.CLI", Except("AgentUp.CLI", "AgentUp.Capabilities.Abstractions"));
        AssertDoesNotDependOn("AgentUp.Installers", Except("AgentUp.Installers"));
        AssertDoesNotDependOn("AgentUp.InstallerApp", Except("AgentUp.InstallerApp", "AgentUp.Installers", "AgentUp.Capabilities.Abstractions", "AgentUp.Capabilities.Common"));
        AssertDoesNotDependOn("AgentUp.Packaging", Except("AgentUp.Packaging", "AgentUp.Installers"));
        AssertDoesNotDependOn("AgentUp.PackageSmoke", Except("AgentUp.PackageSmoke", "AgentUp.Installers"));
    }

    private static void AssertDoesNotDependOn(string sourceAssembly, IReadOnlyCollection<string> allowedAssemblies)
    {
        var source = Types().That().ResideInAssembly(sourceAssembly).As(sourceAssembly);

        foreach (var forbiddenAssembly in ArchitectureFixture.ProductionProjects.Except(allowedAssemblies))
        {
            var forbidden = Types().That().ResideInAssembly(forbiddenAssembly).As(forbiddenAssembly);
            IArchRule rule = Types().That().Are(source).Should().NotDependOnAny(forbidden)
                .Because($"{sourceAssembly} must not take runtime dependencies on {forbiddenAssembly}")
                .WithoutRequiringPositiveResults();
            rule.Check(ArchitectureFixture.ArchUnitArchitecture);
        }
    }

    private static string[] Except(params string[] allowed) => allowed;
}
