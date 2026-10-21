using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Writes the three report files. The PNG is a capture of the short summary page so it
/// stays legible in a chat; the full detail stays in the HTML.
/// </summary>
public sealed class RunReportWriter(IReportPageCapture capture) : IRunReportWriter
{
    public async Task WriteAsync(string directory, RunReport report, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Join(directory, "report.json"), RunReportJson.Serialize(report), cancellationToken);
        await File.WriteAllTextAsync(Path.Join(directory, "report.html"), RunReportHtml.Full(report), cancellationToken);

        var summaryPath = Path.Join(Path.GetTempPath(), "au-check-summary-" + Guid.NewGuid().ToString("N") + ".html");
        await File.WriteAllTextAsync(summaryPath, RunReportHtml.Summary(report), cancellationToken);
        try
        {
            await capture.CaptureAsync(
                summaryPath,
                Path.Join(directory, "report.png"),
                RunReportHtml.SummaryWidth,
                RunReportHtml.SummaryHeight,
                cancellationToken);
        }
        finally
        {
            _ = DeleteQuietly(summaryPath);
        }
    }

    private static bool DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
