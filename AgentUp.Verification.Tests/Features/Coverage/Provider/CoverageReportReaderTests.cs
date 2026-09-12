using AgentUp.Verification.Features.Coverage.Providers;

namespace AgentUp.Verification.Tests.Features.Coverage.Provider;

[TestFixture]
public sealed class CoverageReportReaderTests
{
    private static string CreateRepository() 
    {
        var root = Path.Join(
            TestContext.CurrentContext.WorkDirectory, "coverage-reports-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteReport(
        string root,
        string relativeDirectory,
        string filename,
        string lines,
        DateTime? writtenAtUtc = null)
    {
        var directory = Path.Join(root, relativeDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, "coverage.cobertura.xml"), $"""
            <coverage>
              <sources><source>{root}</source></sources>
              <packages><package name="P"><classes>
                <class name="C" filename="{filename}"><lines>{lines}</lines></class>
              </classes></package></packages>
            </coverage>
            """);

        if (writtenAtUtc is { } stamp)
            File.SetLastWriteTimeUtc(Path.Join(directory, "coverage.cobertura.xml"), stamp);
    }

    private static CoverageReportReader Reader() => new(new CoberturaReportParser());

    [Test]
    public async Task ReadAsync_returnsEmptyWhenTheReportDirectoryDoesNotExist()
    {
        var report = await Reader().ReadAsync(CreateRepository(), "artifacts/coverage", CancellationToken.None);

        Assert.That(report.Files, Is.Empty);
    }

    [Test]
    public async Task ReadAsync_findsReportsNestedUnderTheReportDirectory()
    {
        // dotnet test writes each run into its own GUID subdirectory.
        var root = CreateRepository();
        WriteReport(root, "artifacts/coverage/server/abc-guid", "AgentUp.Server/Program.cs",
            """<line number="1" hits="1" />""");

        var report = await Reader().ReadAsync(root, "artifacts/coverage", CancellationToken.None);

        Assert.That(report.Find("AgentUp.Server/Program.cs"), Is.Not.Null);
    }

    [Test]
    public async Task ReadAsync_mergesReportsFromSeveralTestProjectsForTheSameFile()
    {
        var root = CreateRepository();
        WriteReport(root, "artifacts/coverage/server/one", "AgentUp.Server/Program.cs",
            """<line number="1" hits="1" /><line number="2" hits="0" />""");
        WriteReport(root, "artifacts/coverage/e2e/two", "AgentUp.Server/Program.cs",
            """<line number="1" hits="0" /><line number="2" hits="5" />""");

        var report = await Reader().ReadAsync(root, "artifacts/coverage", CancellationToken.None);
        var file = report.Find("AgentUp.Server/Program.cs")!;

        Assert.Multiple(() =>
        {
            Assert.That(file.IsCovered(1), Is.True);
            Assert.That(file.IsCovered(2), Is.True, "Covered by the second suite.");
        });
    }

    [Test]
    public async Task ReadAsync_ignoresAnEarlierRunOfTheSameCheck()
    {
        // The runner leaves the previous run's report in place. Its line numbers describe
        // the file before the edit, so merging it in would report lines that no longer
        // exist as uncovered.
        var root = CreateRepository();
        WriteReport(root, "artifacts/coverage/server/first-run", "AgentUp.Server/Program.cs",
            """<line number="7" hits="0" />""", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteReport(root, "artifacts/coverage/server/second-run", "AgentUp.Server/Program.cs",
            """<line number="4" hits="1" />""", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var report = await Reader().ReadAsync(root, "artifacts/coverage", CancellationToken.None);
        var file = report.Find("AgentUp.Server/Program.cs")!;

        Assert.Multiple(() =>
        {
            Assert.That(file.IsCovered(4), Is.True);
            Assert.That(file.LineHits.ContainsKey(7), Is.False, "Line 7 belongs to the superseded run.");
        });
    }

    [Test]
    public async Task ReadAsync_keepsTheNewestRunOfEveryCheck()
    {
        var root = CreateRepository();
        WriteReport(root, "artifacts/coverage/server/stale", "AgentUp.Server/Program.cs",
            """<line number="1" hits="0" />""", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteReport(root, "artifacts/coverage/server/fresh", "AgentUp.Server/Program.cs",
            """<line number="1" hits="1" />""", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        WriteReport(root, "artifacts/coverage/cli/only", "AgentUp.CLI/Entry.cs",
            """<line number="1" hits="1" />""", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = await Reader().ReadAsync(root, "artifacts/coverage", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(report.Find("AgentUp.Server/Program.cs")!.IsCovered(1), Is.True);
            Assert.That(report.Find("AgentUp.CLI/Entry.cs"), Is.Not.Null,
                "A check with a single run is not dropped by the newest-run filter.");
        });
    }

    [Test]
    public async Task ReadAsync_readsAReportSittingDirectlyInTheReportDirectory()
    {
        // A report with no check folder of its own is its own group, so it is neither
        // dropped nor treated as another check's superseded run.
        var root = CreateRepository();
        WriteReport(root, "artifacts/coverage", "AgentUp.Server/Program.cs",
            """<line number="1" hits="1" />""");

        var report = await Reader().ReadAsync(root, "artifacts/coverage", CancellationToken.None);

        Assert.That(report.Find("AgentUp.Server/Program.cs")!.IsCovered(1), Is.True);
    }

    [Test]
    public async Task ReadAsync_keepsARootReportAlongsideACheckFolder()
    {
        var root = CreateRepository();
        WriteReport(root, "artifacts/coverage", "AgentUp.Server/Root.cs",
            """<line number="1" hits="1" />""");
        WriteReport(root, "artifacts/coverage/server/run", "AgentUp.Server/Nested.cs",
            """<line number="1" hits="1" />""");

        var report = await Reader().ReadAsync(root, "artifacts/coverage", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(report.Find("AgentUp.Server/Root.cs"), Is.Not.Null);
            Assert.That(report.Find("AgentUp.Server/Nested.cs"), Is.Not.Null);
        });
    }
}
