using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class SliceTestCoverage
{
    private static readonly (string ProductionFolder, string TestKind)[] RequiredCoverage =
    [
        ("Controllers", "Controller"),
        ("Services", "Unit"),
        ("Models", "Unit"),
        ("Providers", "Provider")
    ];

    private static readonly HashSet<string> KnownCoverageDebt = new(StringComparer.Ordinal)
    {
        "AgentUp.Server/Features/Workspaces/Controllers requires AgentUp.Server.Tests/Features/Workspaces/Controller/*Tests.cs",
        "AgentUp.Server/Features/Mcp/Services requires AgentUp.Server.Tests/Features/Mcp/Unit/*Tests.cs",
        "AgentUp.Server/Features/Ports/Controllers requires AgentUp.Server.Tests/Features/Ports/Controller/*Tests.cs",
        "AgentUp.Server/Features/Ports/Services requires AgentUp.Server.Tests/Features/Ports/Unit/*Tests.cs",
        "AgentUp.Server/Features/Ports/Models requires AgentUp.Server.Tests/Features/Ports/Unit/*Tests.cs",
        "AgentUp.Server/Features/Ports/Providers requires AgentUp.Server.Tests/Features/Ports/Provider/*Tests.cs",
        "AgentUp.Server/Features/Processes/Controllers requires AgentUp.Server.Tests/Features/Processes/Controller/*Tests.cs",
        "AgentUp.Server/Features/Processes/Services requires AgentUp.Server.Tests/Features/Processes/Unit/*Tests.cs",
        "AgentUp.Server/Features/Applications/Controllers requires AgentUp.Server.Tests/Features/Applications/Controller/*Tests.cs",
        "AgentUp.Server/Features/Applications/Services requires AgentUp.Server.Tests/Features/Applications/Unit/*Tests.cs",
        "AgentUp.Server/Features/Capabilities/Controllers requires AgentUp.Server.Tests/Features/Capabilities/Controller/*Tests.cs",
        "AgentUp.Server/Features/Capabilities/Services requires AgentUp.Server.Tests/Features/Capabilities/Unit/*Tests.cs",
        "AgentUp.Capabilities.Common/Features/CapabilityInventory/Models requires AgentUp.Capabilities.Common.Tests/Features/CapabilityInventory/Unit/*Tests.cs",
        "AgentUp.Capabilities.Common/Features/CapabilityDiscovery/Models requires AgentUp.Capabilities.Common.Tests/Features/CapabilityDiscovery/Unit/*Tests.cs",
        "AgentUp.Capabilities.Common/Features/CapabilityDiscovery/Providers requires AgentUp.Capabilities.Common.Tests/Features/CapabilityDiscovery/Provider/*Tests.cs",
        "AgentUp.Desktop/Features/Console/Controllers requires AgentUp.Desktop.Tests/Features/Console/Controller/*Tests.cs",
        "AgentUp.Desktop/Features/Console/Services requires AgentUp.Desktop.Tests/Features/Console/Unit/*Tests.cs",
        "AgentUp.Desktop/Features/Console/Providers requires AgentUp.Desktop.Tests/Features/Console/Provider/*Tests.cs",
        "AgentUp.Desktop/Features/Workspaces/Controllers requires AgentUp.Desktop.Tests/Features/Workspaces/Controller/*Tests.cs",
        "AgentUp.Desktop/Features/Ports/Controllers requires AgentUp.Desktop.Tests/Features/Ports/Controller/*Tests.cs",
        "AgentUp.Desktop/Features/Ports/Services requires AgentUp.Desktop.Tests/Features/Ports/Unit/*Tests.cs",
        "AgentUp.Desktop/Features/Applications/Controllers requires AgentUp.Desktop.Tests/Features/Applications/Controller/*Tests.cs",
        "AgentUp.Desktop/Features/Applications/Services requires AgentUp.Desktop.Tests/Features/Applications/Unit/*Tests.cs",
        "AgentUp.Desktop/Features/FirstRun/Controllers requires AgentUp.Desktop.Tests/Features/FirstRun/Controller/*Tests.cs",
        "AgentUp.Desktop/Features/FirstRun/Services requires AgentUp.Desktop.Tests/Features/FirstRun/Unit/*Tests.cs",
        "AgentUp.CLI/Features/Workspaces/Services requires AgentUp.CLI.Tests/Features/Workspaces/Unit/*Tests.cs",
        "AgentUp.CLI/Features/Workspaces/Models requires AgentUp.CLI.Tests/Features/Workspaces/Unit/*Tests.cs",
        "AgentUp.CLI/Features/Workspaces/Providers requires AgentUp.CLI.Tests/Features/Workspaces/Provider/*Tests.cs"
    };

    [Test]
    public void Feature_slices_have_matching_controller_unit_and_provider_test_coverage()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionProjects
            .Where(project => Directory.Exists(Path.Join(root, project, "Features")))
            .SelectMany(project => FindMissingCoverage(root, project))
            .Where(violation => !KnownCoverageDebt.Contains(violation))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Feature slices with Controllers, Services/Models, or Providers must have matching Controller, Unit, or Provider tests.");
    }

    private static IEnumerable<string> FindMissingCoverage(string root, string project)
    {
        var testProject = project + ".Tests";
        if (!Directory.Exists(Path.Join(root, testProject)))
            yield break;

        var featuresRoot = Path.Join(root, project, "Features");
        foreach (var sliceDirectory in Directory.EnumerateDirectories(featuresRoot))
        {
            var slice = Path.GetFileName(sliceDirectory);
            foreach (var (productionFolder, testKind) in RequiredCoverage)
            {
                var productionPath = Path.Join(sliceDirectory, productionFolder);
                if (!Directory.Exists(productionPath) || !Directory.EnumerateFiles(productionPath, "*.cs", SearchOption.AllDirectories).Any())
                    continue;

                var testPath = Path.Join(root, testProject, "Features", slice, testKind);
                if (!Directory.Exists(testPath) || !Directory.EnumerateFiles(testPath, "*Tests.cs", SearchOption.AllDirectories).Any())
                    yield return $"{project}/Features/{slice}/{productionFolder} requires {testProject}/Features/{slice}/{testKind}/*Tests.cs";
            }
        }
    }
}
