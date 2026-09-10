using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Providers;

namespace AgentUp.Server.Tests.Features.Audit.Provider;

[TestFixture]
public sealed class AuditApplicationIndexProviderTests
{
    [Test]
    public void EncodeApplicationKey_IsStableForSameApplicationName()
    {
        var first = AuditApplicationIndexPathsProvider.EncodeApplicationKey("Asset Studio API");
        var second = AuditApplicationIndexPathsProvider.EncodeApplicationKey("asset studio api");

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Has.Length.EqualTo(16));
    }

    [Test]
    public void IndexLineCodec_RoundTripsCompactEntry()
    {
        var evt = new AuditEvent(
            "evt-1",
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            "frontend",
            "web",
            "load",
            "success",
            "ws-1",
            null,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, string> { ["application"] = "web" },
            [],
            "workspace");

        var line = AuditEventIndexLineCodec.Format(evt, 1234, 256);

        Assert.Multiple(() =>
        {
            Assert.That(AuditEventIndexLineCodec.TryParse(line, out var entry), Is.True);
            Assert.That(entry!.EventId, Is.EqualTo("evt-1"));
            Assert.That(entry.Kind, Is.EqualTo("frontend"));
            Assert.That(entry.Stream, Is.Null);
            Assert.That(entry.Offset, Is.EqualTo(1234));
            Assert.That(entry.Length, Is.EqualTo(256));
        });
    }

    [Test]
    public void IndexEntryMatcher_FiltersKindsStreamsAndCursor()
    {
        var entry = new AuditEventIndexEntry(
            "evt-b",
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            "application",
            "stderr",
            null,
            10,
            20);
        var query = new AuditEventQuery(
            "ws-1",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            10,
            Application: "web",
            Before: DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            BeforeEventId: "evt-c",
            Kinds: ["application"],
            Streams: ["stderr"]);

        Assert.That(AuditEventIndexEntryMatcher.Matches(query, entry), Is.True);
    }

    [Test]
    public async Task OffsetLineReader_ReadsLineAtStoredOffset()
    {
        var path = Path.Join(Path.GetTempPath(), $"audit-offset-{Guid.NewGuid():N}.jsonl");
        try
        {
            var appended = await AuditEventOffsetLineReader.AppendLineAsync(path, """{"EventId":"evt-1"}""", CancellationToken.None);
            Assert.That(appended, Is.Not.Null);

            var line = await AuditEventOffsetLineReader.ReadLineAsync(
                path,
                appended!.Value.Offset,
                appended.Value.Length,
                CancellationToken.None);

            Assert.That(line, Is.EqualTo("""{"EventId":"evt-1"}"""));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
