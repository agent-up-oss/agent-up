using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;

namespace AgentUp.CommitPolicy.Tests.Features.CommitPolicy.Provider;

[TestFixture]
public sealed class CommitPolicyProviderTests
{
    private readonly CommitPolicyProvider _provider = new();

    [Test]
    public void Validate_rejectsUnsupportedConventionalCommitPrefix()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate("Commits", "build(Commits): cover queue", ["a.cs"]));

        Assert.That(ex!.Message, Does.Contain("feat, fix, test, chore, refactor, style, docs"));
    }

    [Test]
    public void Validate_rejectsCommitMessageWithoutSliceScope()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate("Commits", "fix: validate queue", ["a.cs"]));

        Assert.That(ex!.Message, Does.Contain("must include a scope"));
    }

    [Test]
    public void Validate_rejectsCommitMessageScopeThatDoesNotMatchQueuedSlice()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate("Commits", "fix(Workspaces): validate queue", ["a.cs"]));

        Assert.That(ex!.Message, Does.Contain("does not match queued slice"));
    }

    [Test]
    public void Validate_rejectsDocsCommitWithRuntimeFile()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate("Commits", "docs(Commits): explain queue", ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"]));

        Assert.That(ex!.Message, Does.Contain("docs commits may only include documentation files"));
    }

    [Test]
    public void Validate_allowsDocsCommitWithDocumentationFiles()
    {
        Assert.DoesNotThrow(() =>
            _provider.Validate("guidance", "docs(guidance): explain queue", ["docs/developer-guide/mcp.md", "AGENTS.md"]));
    }

    [Test]
    public void Validate_rejectsStyleCommitWithNonStyleFile()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate("ui", "style(ui): tune layout", ["src/MainViewModel.cs"]));

        Assert.That(ex!.Message, Does.Contain("style commits may only include CSS or HTML files"));
    }

    [Test]
    public void Validate_allowsStyleCommitWithCssAndHtmlFiles()
    {
        Assert.DoesNotThrow(() =>
            _provider.Validate("docs-style", "style(docs-style): tune layout", ["docs/src/css/custom.css", "docs/static/index.html"]));
    }

    [Test]
    public void Validate_allowsTestCommitWithSmokeValidationFile()
    {
        Assert.DoesNotThrow(() =>
            _provider.Validate("SmokeRuns", "test(SmokeRuns): cover package smoke validation", ["AgentUp.PackageSmoke/Features/SmokeRuns/Services/SmokeRunService.cs"]));
    }

    [Test]
    public void Validate_rejectsTestCommitWithProductionFile()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate("Commits", "test(Commits): cover queue validation", ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"]));

        Assert.That(ex!.Message, Does.Contain("test commits may only include test or smoke-validation files"));
    }

    [Test]
    public void Validate_allowsChoreCommitWithMaintenanceFiles()
    {
        Assert.DoesNotThrow(() =>
            _provider.Validate("ci", "chore(ci): update release workflow", [".github/workflows/release.yml", "packaging/linux/server.service"]));
    }

    [Test]
    public void Validate_rejectsChoreCommitWithSourceFile()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate("Commits", "chore(Commits): tune queue internals", ["src/CommitsService.cs"]));

        Assert.That(ex!.Message, Does.Contain("chore commits may only include maintenance"));
    }

    [Test]
    public void Validate_rejectsFilesThatSpanMultipleFeatureSlices()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _provider.Validate(
                "Commits",
                "fix(Commits): queue",
                [
                    "AgentUp.Server/Features/Commits/Services/CommitsService.cs",
                    "AgentUp.Server/Features/Workspaces/Services/WorkspaceRegistry.cs"
                ]));

        Assert.That(ex!.Message, Does.Contain("multiple feature slices"));
    }

    [Test]
    public void Validate_allowsSingleFeatureSliceWithMatchingTests()
    {
        Assert.DoesNotThrow(() =>
            _provider.Validate(
                "fix/commits",
                "fix(commits): guard queue",
                [
                    "AgentUp.Server/Features/Commits/Services/CommitsService.cs",
                    "AgentUp.Server.Tests/Features/Commits/Unit/CommitsServiceTests.cs"
                ]));
    }

    [Test]
    public void Validate_ignoresLowercaseFeaturesDirectory()
    {
        Assert.DoesNotThrow(() =>
            _provider.Validate(
                "web",
                "fix(web): update feature assets",
                [
                    "web/features/search/SearchPanel.tsx",
                    "web/features/cart/CartPanel.tsx"
                ]));
    }
}
