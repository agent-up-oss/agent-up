using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AgentUp.Architecture.Tests.Fixtures;
using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Models;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class QaCoverageMap
{
    private static readonly string[] RequiredSurfaces =
    [
        "First run / connect",
        "First run / Demo",
        "Workspace list",
        "Workspace lifecycle",
        "Application tabs",
        "Console",
        "Diagnostics",
        "Git review",
        "Git history",
        "Commits",
        "Agents",
        "Autonomous loop",
        "Forge review",
        "Capabilities",
        "Validation panel",
        "Browser isolation",
        "Settings",
        "Example Web app",
        "Example API",
        "Installer"
    ];

    private static readonly Regex Status = new(
        "^(covered|gap|blocked by #\\d+|n/a)$",
        RegexOptions.CultureInvariant);

    private static readonly string[] FirstSuiteDesktop =
    [
        "connect",
        "workspace-lifecycle",
        "console-diagnostics",
        "git-review",
        "validation-panel",
        "agents",
        "autonomous-loop",
        "forge-hosting"
    ];

    private static readonly string[] FirstSuiteMobile =
    [
        "connect",
        "workspace-lifecycle",
        "git-review",
        "agents",
        "autonomous-loop"
    ];

    [Test]
    public void Map_listsEveryRequiredSurfaceWithAStatusInEachClientColumn()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var path = Path.Join(root, "docs/developer-guide/verification/qa-coverage.md");
        var source = File.ReadAllText(path);
        var rows = ParseTable(source);

        Assert.That(rows.Select(row => row.Surface).ToArray(), Is.EqualTo(RequiredSurfaces));
        var empty = rows
            .SelectMany(row => new[]
            {
                string.IsNullOrWhiteSpace(row.Desktop) ? $"{row.Surface} Desktop" : null,
                string.IsNullOrWhiteSpace(row.Mobile) ? $"{row.Surface} Mobile" : null
            })
            .Where(item => item is not null)
            .ToArray();
        Assert.That(empty, Is.Empty, "QA coverage map cells cannot be empty.");
        var invalid = rows
            .SelectMany(row => new[] { row.Desktop, row.Mobile }.Select(status => (row.Surface, status)))
            .Where(item => !Status.IsMatch(item.status.Trim()))
            .Select(item => $"{item.Surface}: '{item.status}'")
            .ToArray();
        Assert.That(invalid, Is.Empty, "Every cell must be covered, gap, blocked by #N, or n/a.");
        var justification = source.Contains("## n/a", StringComparison.Ordinal)
            ? source[source.IndexOf("## n/a", StringComparison.Ordinal)..]
            : "";
        var unjustified = rows
            .Where(row => string.Equals(row.Desktop, "n/a", StringComparison.Ordinal)
                          || string.Equals(row.Mobile, "n/a", StringComparison.Ordinal))
            .Select(row => row.Surface)
            .Where(surface => !justification.Contains(surface, StringComparison.Ordinal))
            .ToArray();
        Assert.That(unjustified, Is.Empty, "Every n/a cell needs a justification that names the surface.");
    }

    [Test]
    public void First_suite_Desktop_flows_exist_and_pass_surface_authoring_rules()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var flows = LoadDesktopFlows(root);
        var missing = FirstSuiteDesktop.Where(id => flows.All(flow => flow.Id != id)).ToArray();
        Assert.That(missing, Is.Empty, "First-suite Desktop flow ids must exist in .agent-up/validation-flows.json.");
        var errors = FirstSuiteDesktop
            .Select(id => flows.First(flow => flow.Id == id))
            .Select(flow => (flow.Id, Error: DesktopSurfaceFlowRules.Validate(ToSave(flow))))
            .Where(item => item.Error is not null)
            .Select(item => $"{item.Id}: {item.Error}")
            .ToArray();
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void First_suite_Mobile_flows_exist_and_pass_surface_authoring_rules()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var directory = Path.Join(root, "AgentUp.Mobile.E2E/pwa/flows");
        var missing = FirstSuiteMobile
            .Where(id => !File.Exists(Path.Join(directory, $"{id}.json")))
            .ToArray();
        Assert.That(missing, Is.Empty, "First-suite Mobile flow files must exist under AgentUp.Mobile.E2E/pwa/flows.");
        var errors = FirstSuiteMobile
            .Select(id =>
            {
                var flow = JsonSerializer.Deserialize<PersistedFlow>(
                    File.ReadAllText(Path.Join(directory, $"{id}.json")),
                    JsonOptions())!;
                return (flow.Id, Error: MobileSurfaceFlowRules.Validate(ToSave(flow)));
            })
            .Where(item => item.Error is not null)
            .Select(item => $"{item.Id}: {item.Error}")
            .ToArray();
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Agents_md_requires_the_qa_coverage_map_for_new_user_facing_surfaces()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var agents = File.ReadAllText(Path.Join(root, "AGENTS.md"));
        Assert.That(agents, Does.Contain("docs/developer-guide/verification/qa-coverage.md"));
        Assert.That(agents, Does.Contain("user-facing surface"));
    }

    private static IReadOnlyList<PersistedFlow> LoadDesktopFlows(string root)
    {
        var json = File.ReadAllText(Path.Join(root, ".agent-up/validation-flows.json"));
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("flows")
            .EnumerateArray()
            .Select(element => element.Deserialize<PersistedFlow>(JsonOptions())!)
            .Where(flow => string.Equals(flow.Surface, "Desktop", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(flow.Application, "Desktop", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static SaveValidationFlowRequest ToSave(PersistedFlow flow) =>
        new(
            flow.Id,
            flow.Application,
            flow.Name,
            flow.Description ?? "",
            flow.InitialPath,
            flow.InitialExpectations,
            flow.Steps,
            Enum.TryParse<ValidationSurface>(flow.Surface, true, out var surface) ? surface : ValidationSurface.Application);

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static IReadOnlyList<MapRow> ParseTable(string source)
    {
        var rows = new List<MapRow>();
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|') || line.Contains("---", StringComparison.Ordinal))
                continue;
            var cells = line.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length < 4 || cells[0] is "Surface")
                continue;
            rows.Add(new MapRow(cells[0], cells[2], cells[3]));
        }

        return rows;
    }

    private sealed record MapRow(string Surface, string Desktop, string Mobile);

    private sealed record PersistedFlow(
        string Id,
        string Application,
        string Name,
        string? Description,
        string InitialPath,
        IReadOnlyList<ValidationAssertion> InitialExpectations,
        IReadOnlyList<ValidationStep> Steps,
        string? Surface);
}
