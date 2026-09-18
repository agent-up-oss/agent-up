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
