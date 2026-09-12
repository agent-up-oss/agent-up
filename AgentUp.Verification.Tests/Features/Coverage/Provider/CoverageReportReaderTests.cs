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

    private static void WriteReport(string root, string relativeDirectory, string filename, string lines)
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
}
