using AgentUp.AUDebug.Features.Docs.Providers;
using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Tests.Features.Docs.Provider;

[TestFixture]
public sealed class DocsPageUrlProviderTests
{
    [Test]
    public void Resolve_defaultsToUserDocsHome()
    {
        Assert.That(DocsPageUrlProvider.Resolve(null), Is.EqualTo($"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}"));
        Assert.That(DocsPageUrlProvider.Resolve("  "), Is.EqualTo($"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}"));
    }

    [Test]
    public void Resolve_prefixesBareUserDocsPaths()
    {
        Assert.That(DocsPageUrlProvider.Resolve("workspaces"), Is.EqualTo($"{DebugLayout.DocsUrl}/docs/workspaces"));
        Assert.That(DocsPageUrlProvider.Resolve("docs/workspaces"), Is.EqualTo($"{DebugLayout.DocsUrl}/docs/workspaces"));
        Assert.That(DocsPageUrlProvider.Resolve("/docs/workspaces"), Is.EqualTo($"{DebugLayout.DocsUrl}/docs/workspaces"));
    }

    [Test]
    public void Resolve_keepsDeveloperGuideAndDesignSystem()
    {
        Assert.That(
            DocsPageUrlProvider.Resolve("/developer-guide/git"),
            Is.EqualTo($"{DebugLayout.DocsUrl}/developer-guide/git"));
        Assert.That(
            DocsPageUrlProvider.Resolve("developer-guide/git#what-it-is"),
            Is.EqualTo($"{DebugLayout.DocsUrl}/developer-guide/git#what-it-is"));
        Assert.That(
            DocsPageUrlProvider.Resolve("/design-system#catalog"),
            Is.EqualTo($"{DebugLayout.DocsUrl}/design-system#catalog"));
        Assert.That(
            DocsPageUrlProvider.Resolve("#what-it-is"),
            Is.EqualTo($"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}#what-it-is"));
    }

    [Test]
    public void Resolve_rejectsUnsafeOrUnknownPaths()
    {
        Assert.That(() => DocsPageUrlProvider.Resolve("../secret"), Throws.InvalidOperationException);
        Assert.That(() => DocsPageUrlProvider.Resolve("http://example.invalid/docs"), Throws.InvalidOperationException);
        Assert.That(() => DocsPageUrlProvider.Resolve("/api/auth"), Throws.InvalidOperationException);
        Assert.That(() => DocsPageUrlProvider.Resolve("//evil.example/docs"), Throws.InvalidOperationException);
        Assert.That(() => DocsPageUrlProvider.Resolve("/docs/workspaces?q=1"), Throws.InvalidOperationException);
    }
}
