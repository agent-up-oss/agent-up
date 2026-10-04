using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

/// <summary>
/// The join between the screens the design system documents and the ones the clients showed.
/// </summary>
[TestFixture]
public sealed class ScreenComparisonProviderTests
{
    [Test]
    public void Compare_realScreenShowingEveryDocumentedString_matches()
    {
        var result = Compare(
            Documented("mobile-git", "Git", "Commit message"),
            Captured("mobile-git", text: "Git\nChanges\nCommit message\nCommit"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ScreenComparisonStatus.Matched));
            Assert.That(result.MissingCopy, Is.Empty);
        });
    }

    [Test]
    public void Compare_realScreenMissingDocumentedCopy_divergesAndNamesIt()
    {
        var result = Compare(
            Documented("mobile-history", "History", "feat(storefront): add the weekly promo banner", "Demo"),
            Captured("mobile-history", text: "History\n02.10.26 14:33"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ScreenComparisonStatus.Diverged));
            Assert.That(result.MissingCopy, Is.EqualTo(new[] { "feat(storefront): add the weekly promo banner", "Demo" }));
        });
    }

    [Test]
    public void Compare_copyTheRealScreenWrapped_stillMatches()
    {
        var result = Compare(
            Documented("mobile-agents", "Start another session"),
            Captured("mobile-agents", text: "Start another\n  session"));

        Assert.That(result.Status, Is.EqualTo(ScreenComparisonStatus.Matched));
    }

    [Test]
    public void Compare_copyDifferingOnlyInCase_diverges()
    {
        var result = Compare(
            Documented("mobile-git", "Commit message"),
            Captured("mobile-git", text: "COMMIT MESSAGE"));

        Assert.That(result.Status, Is.EqualTo(ScreenComparisonStatus.Diverged),
            "The design system decides whether a label is a field label or a title, so a case change is drift.");
    }

    [Test]
    public void Compare_documentedScreenTheRunNeverCaptured_isReportedNotCaptured()
    {
        var results = new ScreenComparisonProvider().Compare([Documented("mobile-agent", "Send")], []);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Status, Is.EqualTo(ScreenComparisonStatus.NotCaptured));
            Assert.That(results[0].Detail, Does.Contain("captured nothing"));
        });
    }

    [Test]
    public void Compare_screenTheRunSkipped_carriesTheReasonRatherThanPassing()
    {
        var result = Compare(
            Documented("desktop-database", "orders"),
            Captured("desktop-database", text: null, skipped: "Demo reports no Database entitlement."));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ScreenComparisonStatus.Skipped));
            Assert.That(result.Detail, Is.EqualTo("Demo reports no Database entitlement."));
        });
    }

    [Test]
    public void Compare_surfaceWithNoTextChannel_isNotComparableRatherThanMatched()
    {
        var result = Compare(
            Documented("desktop-git", "Git changes"),
            Captured("desktop-git", text: null, surface: "desktop"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ScreenComparisonStatus.NotComparable),
                "Reporting a match for a screen nothing was read from would be the gate passing without checking.");
            Assert.That(result.Detail, Does.Contain("reports no text"));
        });
    }

    [Test]
    public void Compare_everyDocumentedScreenGetsExactlyOneResult()
    {
        var results = new ScreenComparisonProvider().Compare(
            [Documented("mobile-git", "Git"), Documented("mobile-history", "History")],
            [Captured("mobile-git", "Git"), Captured("mobile-history", "History"), Captured("mobile-apps", "Apps")]);

        Assert.That(results.Select(result => result.Id), Is.EqualTo(new[] { "mobile-git", "mobile-history" }));
    }

    private static ScreenComparisonDto Compare(ScreenshotSceneCopyDto documented, ScreenCaptureDto captured)
        => new ScreenComparisonProvider().Compare([documented], [captured])[0];

    private static ScreenshotSceneCopyDto Documented(string id, params string[] copy)
        => new(id, id.Split('-')[0], copy);

    private static ScreenCaptureDto Captured(
        string id,
        string? text,
        string? skipped = null,
        string surface = "mobile")
        => new(id, surface, id.Split('-', 2)[1], id, skipped is null ? $"{id}.png" : null, skipped, text);
}
