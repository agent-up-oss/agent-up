using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Diagnostics.Models;
using AgentUp.Server.Features.Diagnostics.Providers;
using AgentUp.Server.Features.Verification.DTOs;

namespace AgentUp.Server.Tests.Features.Diagnostics.Provider;

[TestFixture]
public sealed class ReviewSnapshotHtmlProviderTests
{
    [Test]
    public void Render_EmbedsCompiledDesignSystemCssAndWorkspaceIdentity()
    {
        var html = Render(Snapshot());

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.StartWith("<!DOCTYPE html>"));
            Assert.That(html, Does.Contain("--au-color-canvas"));
            Assert.That(html, Does.Contain("class=\"au-page-title\""));
            Assert.That(html, Does.Contain("Harbor Shop"));
            Assert.That(html, Does.Contain("/demo/harbor-shop"));
            Assert.That(html, Does.Contain("Export Report"));
        });
    }

    [Test]
    public void Render_EncodesHostileCopyAndStillShowsVerificationAndScreenshots()
    {
        var html = Render(Snapshot() with
        {
            DisplayName = "<script>alert(1)</script>",
            Screenshots = [],
            Verification = null
        });

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("<script>alert(1)</script>"));
            Assert.That(html, Does.Contain("&lt;script&gt;alert(1)&lt;/script&gt;"));
            Assert.That(html, Does.Contain("<h2>Screenshots</h2>"));
            Assert.That(html, Does.Contain("<h2>Verification</h2>"));
            Assert.That(html, Does.Contain("Verification could not run"));
            Assert.That(html, Does.Contain("No screenshots."));
        });
    }

    [Test]
    public void Render_EmbedsScreenshotBytesAndPassedChecks()
    {
        var html = Render(Snapshot() with
        {
            Screenshots =
            [
                new ReviewScreenshotSnapshot(
                    "evt",
                    "shot1",
                    DateTimeOffset.Parse("2026-04-11T15:30:00Z"),
                    "http://127.0.0.1:4010/",
                    "image/png",
                    Convert.ToBase64String([1, 2, 3]))
            ],
            Verification = new VerificationExportDto(
                "abc123",
                "sha256:deadbeef",
                "2026-04-11T15:31:00Z",
                2,
                [],
                [
                    new VerificationExportCheckDto("architecture", "dotnet test", "passed", "ok", 0, "2026-04-11T15:31:00Z", 12, ""),
                    new VerificationExportCheckDto("mobile", "npm test", "neverRun", "", null, null, null, "")
                ])
        });

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("data:image/png;base64,AQID"));
            Assert.That(html, Does.Contain("http://127.0.0.1:4010/"));
            Assert.That(html, Does.Contain("sha256:deadbeef"));
            Assert.That(html, Does.Contain("architecture"));
            Assert.That(html, Does.Contain("au-badge--healthy"));
            Assert.That(html, Does.Contain("neverRun"));
            Assert.That(html, Does.Contain("1 passed, 0 failed, 0 skipped, 1 never run"));
        });
    }

    [Test]
    public void Load_ReturnsTheCompiledStylesheet()
    {
        var css = new ReviewSnapshotCssProvider().Load();
        Assert.That(css, Does.Contain("--au-color-canvas").And.Contain(".au-overview"));
    }

    private static string Render(WorkspaceReviewSnapshot snapshot)
        => new ReviewSnapshotHtmlProvider(new ReviewSnapshotCssProvider()).Render(snapshot);

    private static WorkspaceReviewSnapshot Snapshot()
        => new(
            "harbor-shop",
            "Harbor Shop",
            "/demo/harbor-shop",
            "/demo/harbor-shop",
            "main",
            "abc123",
            "Running",
            "Healthy",
            DateTimeOffset.Parse("2026-04-11T15:30:00Z"),
            "http://127.0.0.1:4010/",
            4.2,
            256 * 1024 * 1024,
            50 * 1024 * 1024,
            2,
            [
                new ReviewApplicationSnapshot(
                    "Storefront",
                    "Process",
                    "Running",
                    "Healthy",
                    "port",
                    [new ReviewPortSnapshot("WEB_PORT", 4010, "http")],
                    ["listening"],
                    false)
            ],
            [
                new DiagnosticEntryDto(
                    "e1",
                    DateTimeOffset.Parse("2026-04-11T15:29:00Z"),
                    "health",
                    "info",
                    "resolved",
                    "server",
                    "port_health_check",
                    "Storefront",
                    null,
                    "http://localhost:4010/health",
                    new Dictionary<string, string>())
            ],
            [
                new ReviewActionSnapshot(
                    "a1",
                    DateTimeOffset.Parse("2026-04-11T15:28:00Z"),
                    "mcp",
                    "orchestration",
                    "start_workspace",
                    "success",
                    "started")
            ],
            [],
            null);
}
