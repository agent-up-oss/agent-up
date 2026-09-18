using System.Text.RegularExpressions;
using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class DocumentationConsistency
{
    private static readonly string[] RetiredPhrases =
    [
        "agent-up register",
        "update_commit_tests",
        "opaque shell commands",
        "shared browser session",
    ];

    private static readonly string[] FrozenSlices =
    [
        "Workspaces",
        "Applications",
        "Git",
        "Commits",
        "Agents",
        "Browser",
        "Diagnostics",
        "Verification",
        "Configuration",
    ];

    private static readonly string[] ForbiddenTopLevelNames =
    [
        "Desktop",
        "Mobile",
        "Server",
        "CLI",
        "Packaging",
        "CI",
    ];

    [Test]
    public void Definition_sources_do_not_use_retired_phrases()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var files = EnumerateDefinitionFiles(root);
        var violations = files
            .SelectMany(path => RetiredPhrases
                .Where(phrase => File.ReadAllText(path).Contains(phrase, StringComparison.Ordinal))
                .Select(phrase => $"{ArchitectureFixture.Relative(root, path)}: {phrase}"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Replace retired phrases with the glossary in AGENTS.md.");
    }

    [Test]
    public void User_docs_sidebar_matches_frozen_slice_list()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        AssertSidebar(
            root,
            Path.Join(root, "docs/sidebars.js"),
            allowedExtra: "Start",
            docsRoot: Path.Join(root, "docs/user-docs"));
    }

    [Test]
    public void Developer_guide_sidebar_matches_frozen_slice_list()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        AssertSidebar(
            root,
            Path.Join(root, "docs/sidebarsDeveloper.js"),
            allowedExtra: "Repo",
            docsRoot: Path.Join(root, "docs/developer-guide"));
    }

    [Test]
    public void Footer_lists_the_frozen_slices()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var config = File.ReadAllText(Path.Join(root, "docs/docusaurus.config.js"));
        var footer = config[config.IndexOf("footer:", StringComparison.Ordinal)..];
        var missing = FrozenSlices
            .Where(slice => !footer.Contains($"label: '{slice}'", StringComparison.Ordinal))
            .ToArray();
        Assert.That(missing, Is.Empty, "docs/docusaurus.config.js footer must list every frozen slice.");
        var forbidden = ForbiddenTopLevelNames
            .Where(name => Regex.IsMatch(footer, $@"label:\s*'{Regex.Escape(name)}'", RegexOptions.Ordinal))
            .ToArray();
        Assert.That(forbidden, Is.Empty,
            "Footer items cannot be named Desktop, Mobile, Server, CLI, Packaging, or CI.");
    }

    private static void AssertSidebar(string root, string sidebarPath, string allowedExtra, string docsRoot)
    {
        var source = File.ReadAllText(sidebarPath);
        var categories = CategoryLabels(source);
        Assert.That(categories, Does.Contain(allowedExtra),
            $"{ArchitectureFixture.Relative(root, sidebarPath)} must include the {allowedExtra} category.");
        var extras = categories.Except(FrozenSlices, StringComparer.Ordinal).ToArray();
        Assert.That(extras, Is.EqualTo(new[] { allowedExtra }),
            "Exactly one non-slice category is allowed.");
        var slices = categories.Where(label => FrozenSlices.Contains(label, StringComparer.Ordinal)).ToArray();
        Assert.That(slices, Is.EqualTo(FrozenSlices),
            "Sidebar slice categories must match the frozen Docs IA list in order.");

        var forbiddenCategories = categories
            .Where(label => ForbiddenTopLevelNames.Contains(label, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        Assert.That(forbiddenCategories, Is.Empty,
            "Client and platform names cannot be sidebar categories.");

        var topLevelMarkdown = Directory.GetFiles(docsRoot, "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.Equals(name, "index", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.That(topLevelMarkdown, Is.Empty,
            $"{docsRoot} may only keep index.md at the top level; slice pages belong in slice folders.");

        var forbiddenFolders = Directory.GetDirectories(docsRoot)
            .Select(Path.GetFileName)
            .Where(name => ForbiddenTopLevelNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                || ForbiddenTopLevelNames.Contains(ToTitle(name), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        Assert.That(forbiddenFolders, Is.Empty,
            "Top-level docs folders cannot be named Desktop, Mobile, Server, CLI, Packaging, or CI.");
    }

    private static string[] CategoryLabels(string source)
        => Regex.Matches(source, @"type:\s*'category',\s*label:\s*'([^']+)'", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static string ToTitle(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name[1..];

    private static IEnumerable<string> EnumerateDefinitionFiles(string root)
    {
        var roots = new[]
        {
            "README.md",
            "CONTRIBUTING.md",
            "docs/user-docs",
            "docs/developer-guide",
            "AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml",
            "AgentUp.DesignSystem/src/catalog.html",
            "AgentUp.DesignSystem/brand/voice.json",
        }.Select(relative => Path.Join(root, relative));

        return roots.Where(File.Exists)
            .Concat(roots.Where(Directory.Exists)
                .SelectMany(path => Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories)
                    .Where(file => !file.EndsWith("-assessment.md", StringComparison.Ordinal))));
    }
}
