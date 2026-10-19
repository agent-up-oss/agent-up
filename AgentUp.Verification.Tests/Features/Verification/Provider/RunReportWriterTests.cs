using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class RunReportWriterTests
{
    [Test]
    public async Task WriteAsync_writesJsonHtmlAndCapturesTheSummaryPage()
    {
        var directory = Path.Join(TestContext.CurrentContext.WorkDirectory, "report-" + Guid.NewGuid().ToString("N"));
        var capture = new ReadingCapture();
        var report = new RunReport(
            "0.0.0",
            "/repo/agent-up.json",
            "sha256:abc",
            "abc123",
            ["src/A.cs"],
            [new RunReportCheck("image", "fast", "docker info", "skipped", null, "toolchainMissing", "docker is not installed", null)],
            0,
            0,
            0,
            1,
            2,
            "toolchainMissing");

        await new RunReportWriter(capture).WriteAsync(directory, report, CancellationToken.None);

        var json = await File.ReadAllTextAsync(Path.Join(directory, "report.json"));
        var html = await File.ReadAllTextAsync(Path.Join(directory, "report.html"));
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"couldNotRun\": 1"));
            Assert.That(json, Does.Contain("toolchainMissing"));
            Assert.That(html.IndexOf("id=\"could-not-run\"", StringComparison.Ordinal),
                Is.LessThan(html.IndexOf("id=\"failures\"", StringComparison.Ordinal)));
            Assert.That(capture.Html, Does.Contain("docker is not installed"));
            Assert.That(capture.Width, Is.EqualTo(RunReportHtml.SummaryWidth));
            Assert.That(File.ReadAllBytes(Path.Join(directory, "report.png")).Length, Is.GreaterThan(0));
        });
    }

    [Test]
    public void CaptureAsync_stopsAChromiumThatDoesNotExit()
    {
        var directory = Path.Join(TestContext.CurrentContext.WorkDirectory, "chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Join(directory, "chromium");
        File.WriteAllText(executable, "#!/bin/sh\nsleep 30\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var capture = new ChromiumReportPageCapture(TimeSpan.FromMilliseconds(200), directory);

        var exception = Assert.ThrowsAsync<ReportCaptureException>(async () =>
            await capture.CaptureAsync("/tmp/summary.html", "/tmp/report.png", 1200, 720, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("did not finish"));
    }

    [Test]
    public void ChromiumArguments_matchTheProductScreenCaptureFlags()
    {
        var arguments = ChromiumReportPageCapture.Arguments("/tmp/summary.html", "/tmp/report.png", "/tmp/profile", 1200, 720);

        Assert.Multiple(() =>
        {
            Assert.That(arguments, Does.Contain("--headless=new"));
            Assert.That(arguments, Does.Contain("--screenshot=/tmp/report.png"));
            Assert.That(arguments, Does.Contain("--window-size=1200,720"));
            Assert.That(arguments, Does.Contain("file:///tmp/summary.html"));
        });
    }

    private sealed class ReadingCapture : IReportPageCapture
    {
        public string Html { get; private set; } = "";
        public int Width { get; private set; }

        public async Task CaptureAsync(string htmlPath, string pngPath, int width, int height, CancellationToken cancellationToken)
        {
            Html = await File.ReadAllTextAsync(htmlPath, cancellationToken);
            Width = width;
            await File.WriteAllBytesAsync(pngPath, [0x89, 0x50, 0x4E, 0x47], cancellationToken);
        }
    }
}
