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
        "AgentUp.CLI/Features/Workspaces/Providers requires AgentUp.CLI.Tests/Features/Workspaces/Provider/*Tests.cs",
        "AgentUp.Installers/Features/WindowsInstallation/Controllers requires AgentUp.Installers.Tests/Features/WindowsInstallation/Controller/*Tests.cs",
        "AgentUp.Installers/Features/WindowsInstallation/Services requires AgentUp.Installers.Tests/Features/WindowsInstallation/Unit/*Tests.cs",
        "AgentUp.Installers/Features/WindowsInstallation/Models requires AgentUp.Installers.Tests/Features/WindowsInstallation/Unit/*Tests.cs",
        "AgentUp.Installers/Features/NixOsInstallation/Controllers requires AgentUp.Installers.Tests/Features/NixOsInstallation/Controller/*Tests.cs",
        "AgentUp.Installers/Features/NixOsInstallation/Services requires AgentUp.Installers.Tests/Features/NixOsInstallation/Unit/*Tests.cs",
        "AgentUp.Installers/Features/PrerequisiteChecks/Controllers requires AgentUp.Installers.Tests/Features/PrerequisiteChecks/Controller/*Tests.cs",
        "AgentUp.Installers/Features/PrerequisiteChecks/Services requires AgentUp.Installers.Tests/Features/PrerequisiteChecks/Unit/*Tests.cs",
        "AgentUp.Installers/Features/PrerequisiteChecks/Models requires AgentUp.Installers.Tests/Features/PrerequisiteChecks/Unit/*Tests.cs",
        "AgentUp.Installers/Features/UbuntuInstallation/Controllers requires AgentUp.Installers.Tests/Features/UbuntuInstallation/Controller/*Tests.cs",
        "AgentUp.Installers/Features/Installation/Controllers requires AgentUp.Installers.Tests/Features/Installation/Controller/*Tests.cs",
        "AgentUp.Installers/Features/MacOsInstallation/Controllers requires AgentUp.Installers.Tests/Features/MacOsInstallation/Controller/*Tests.cs",
        "AgentUp.Installers/Features/MacOsInstallation/Services requires AgentUp.Installers.Tests/Features/MacOsInstallation/Unit/*Tests.cs",
        "AgentUp.Installers/Features/MacOsInstallation/Models requires AgentUp.Installers.Tests/Features/MacOsInstallation/Unit/*Tests.cs",
        "AgentUp.InstallerApp/Features/Logging/Controllers requires AgentUp.InstallerApp.Tests/Features/Logging/Controller/*Tests.cs",
        "AgentUp.InstallerApp/Features/Logging/Services requires AgentUp.InstallerApp.Tests/Features/Logging/Unit/*Tests.cs",
        "AgentUp.InstallerApp/Features/Capabilities/Controllers requires AgentUp.InstallerApp.Tests/Features/Capabilities/Controller/*Tests.cs",
        "AgentUp.InstallerApp/Features/Capabilities/Services requires AgentUp.InstallerApp.Tests/Features/Capabilities/Unit/*Tests.cs",
        "AgentUp.InstallerApp/Features/Capabilities/Models requires AgentUp.InstallerApp.Tests/Features/Capabilities/Unit/*Tests.cs",
        "AgentUp.InstallerApp/Features/Capabilities/Providers requires AgentUp.InstallerApp.Tests/Features/Capabilities/Provider/*Tests.cs",
        "AgentUp.Packaging/Features/UbuntuPackages/Controllers requires AgentUp.Packaging.Tests/Features/UbuntuPackages/Controller/*Tests.cs",
        "AgentUp.Packaging/Features/WindowsPackages/Controllers requires AgentUp.Packaging.Tests/Features/WindowsPackages/Controller/*Tests.cs",
        "AgentUp.Packaging/Features/WindowsPackages/Services requires AgentUp.Packaging.Tests/Features/WindowsPackages/Unit/*Tests.cs",
        "AgentUp.Packaging/Features/WindowsPackages/Models requires AgentUp.Packaging.Tests/Features/WindowsPackages/Unit/*Tests.cs",
        "AgentUp.Packaging/Features/MacOsPackages/Controllers requires AgentUp.Packaging.Tests/Features/MacOsPackages/Controller/*Tests.cs",
        "AgentUp.PackageSmoke/Features/PackageValidation/Controllers requires AgentUp.PackageSmoke.Tests/Features/PackageValidation/Controller/*Tests.cs",
        "AgentUp.PackageSmoke/Features/InstalledServiceValidation/Controllers requires AgentUp.PackageSmoke.Tests/Features/InstalledServiceValidation/Controller/*Tests.cs",
        "AgentUp.PackageSmoke/Features/SmokeRuns/Services requires AgentUp.PackageSmoke.Tests/Features/SmokeRuns/Unit/*Tests.cs",
        "AgentUp.PackageSmoke/Features/SmokeRuns/Providers requires AgentUp.PackageSmoke.Tests/Features/SmokeRuns/Provider/*Tests.cs",
        "AgentUp.PackageSmoke/Features/InstallerFlowValidation/Controllers requires AgentUp.PackageSmoke.Tests/Features/InstallerFlowValidation/Controller/*Tests.cs",
        "AgentUp.PackageSmoke/Features/InstallerFlowValidation/Services requires AgentUp.PackageSmoke.Tests/Features/InstallerFlowValidation/Unit/*Tests.cs",
        "AgentUp.PackageSmoke/Features/RuntimeSecurity/Controllers requires AgentUp.PackageSmoke.Tests/Features/RuntimeSecurity/Controller/*Tests.cs",
        "AgentUp.PackageSmoke/Features/RuntimeSecurity/Services requires AgentUp.PackageSmoke.Tests/Features/RuntimeSecurity/Unit/*Tests.cs"
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
