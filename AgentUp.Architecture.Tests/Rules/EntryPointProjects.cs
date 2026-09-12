using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Rules over the projects that exist only to compose others into an executable.
/// </summary>
/// <remarks>
/// AgentUp.InstallerApp, AgentUp.Packaging and AgentUp.PackageSmoke have no test projects,
/// and that is a decision rather than an oversight: each is a Program.cs that hands
/// manifests to a LocalInstaller builder, so a test could only mirror the builder chain.
/// The decision holds only while they stay that shape, which is what this rule checks. A
/// file that appears here carrying real logic has to move into a project that is tested, or
/// bring a test project with it.
/// </remarks>
[TestFixture]
public sealed class EntryPointProjects
{
    [Test]
    public void Composition_only_projects_contain_nothing_but_an_entrypoint_and_manifests()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.CompositionOnlyProjects
            .SelectMany(project => SourceFilesOf(root, project))
            .Where(parts => !IsEntryPointOrManifest(parts))
            .Select(parts => string.Join('/', parts))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            "These projects are untested because they only compose others. Logic added here "
            + "must move to a tested project, or the project needs a test project of its own.");
    }

    [Test]
    public void Composition_only_projects_have_no_test_project()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);

        // The reverse direction: a test project appearing for one of these means the
        // project grew logic, so it no longer belongs on the composition-only list.
        var unexpected = ArchitectureFixture.CompositionOnlyProjects
            .Where(project => Directory.Exists(Path.Join(root, project + ".Tests")))
            .Select(project => $"{project} has a test project but is listed as composition-only")
            .ToArray();

        Assert.That(unexpected, Is.Empty,
            "Remove the project from CompositionOnlyProjects and add it to ProductionProjects.");
    }

    private static IEnumerable<string[]> SourceFilesOf(string root, string project)
        => Directory.Exists(Path.Join(root, project))
            ? ArchitectureFixture.ProjectSourceFiles(root, project)
                .Select(path => ArchitectureFixture.Parts(root, path))
            : [];

    private static bool IsEntryPointOrManifest(string[] parts)
        => parts.Length == 2 && parts[1] == "Program.cs"
           || parts.Length >= 3 && parts[1] == "Composition";
}
