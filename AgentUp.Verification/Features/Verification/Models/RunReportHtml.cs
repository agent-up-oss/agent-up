using System.Net;
using System.Text;

namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// Self-contained HTML for one run. Tokens match the design-system canvas so the page
/// reads as the product. Nothing in the file is loaded from the network.
/// </summary>
public static class RunReportHtml
{
    public const int SummaryWidth = 1200;
    public const int SummaryHeight = 720;

    public static string Full(RunReport report)
        => Document(report, includePassed: true, truncateOutputAt: 4000);

    public static string Summary(RunReport report)
        => Document(report, includePassed: false, truncateOutputAt: 500);

    private static string Document(RunReport report, bool includePassed, int truncateOutputAt)
    {
        var couldNotRun = report.Checks.Where(check => check.Outcome == "skipped").ToArray();
        var failures = report.Checks.Where(check => check.Outcome == "failed").ToArray();
        var passed = report.Checks.Where(check => check.Outcome == "passed").ToArray();
        var builder = new StringBuilder();
        builder.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title>au-check</title>
            <style>
            :root {
              --au-color-canvas: #0a0b0c;
              --au-color-surface-raised: #191c1f;
              --au-color-text-primary: #f5fbf7;
              --au-color-text-secondary: #c2d2ca;
              --au-color-text-muted: #8a9a92;
              --au-color-text-warning: #ffd66b;
              --au-color-text-danger: #e48989;
              --au-color-accent: #00b850;
              --au-color-status-danger: #d84f4f;
              --au-color-border-subtle: #ffffff14;
            }
            * { box-sizing: border-box; }
            body {
              margin: 0; padding: 32px;
              background: var(--au-color-canvas); color: var(--au-color-text-primary);
              font: 16px/1.45 ui-sans-serif, system-ui, sans-serif;
            }
            h1 { font-size: 28px; font-weight: 700; margin: 0 0 8px; }
            h2 { font-size: 18px; font-weight: 650; margin: 28px 0 8px; }
            .verdict { font-size: 22px; margin: 0 0 16px; }
            .panel {
              background: var(--au-color-surface-raised);
              border: 1px solid var(--au-color-border-subtle);
              border-radius: 12px; padding: 16px 18px; margin: 0 0 12px;
            }
            .warn { color: var(--au-color-text-warning); }
            .bad { color: var(--au-color-text-danger); }
            .ok { color: var(--au-color-accent); }
            .muted { color: var(--au-color-text-muted); }
            pre {
              white-space: pre-wrap; word-break: break-word;
              background: var(--au-color-canvas); padding: 12px; border-radius: 8px;
              color: var(--au-color-text-secondary); font-size: 13px;
            }
            summary { cursor: pointer; }
            </style>
            </head>
            <body>
            """);
        builder.Append("<h1>au-check</h1>");
        builder.Append("<p class=\"verdict\" id=\"verdict\">");
        builder.Append(Encode(
            $"Ran {report.Ran}, passed {report.Passed}, failed {report.Failed}, could not run {report.CouldNotRun}."));
        builder.Append("</p>");
        builder.Append("<p class=\"muted\">");
        builder.Append(Encode($"Exit {report.ExitCode} ({report.ExitReason}). {report.Commit}"));
        builder.Append("</p>");

        builder.Append("<h2 id=\"could-not-run\">Could not run</h2>");
        if (couldNotRun.Length == 0)
            builder.Append("<p class=\"ok\">Every required check could run on this machine.</p>");
        else
        {
            foreach (var check in couldNotRun)
                builder.Append(CheckPanel(check, truncateOutputAt));
        }

        builder.Append("<h2 id=\"failures\">Failures</h2>");
        if (failures.Length == 0)
            builder.Append("<p class=\"ok\">No check failed.</p>");
        else
        {
            var shown = includePassed ? failures : failures.Take(1);
            foreach (var check in shown)
                builder.Append(CheckPanel(check, truncateOutputAt));
            if (!includePassed && failures.Length > 1)
            {
                builder.Append("<p class=\"muted\">");
                builder.Append(Encode($"{failures.Length - 1} more failure(s) are in report.html."));
                builder.Append("</p>");
            }
        }

        if (includePassed)
        {
            builder.Append("<h2 id=\"passed\">Passed</h2><details><summary>");
            builder.Append(Encode($"{passed.Length} passed"));
            builder.Append("</summary>");
            foreach (var check in passed)
                builder.Append(CheckPanel(check, truncateOutputAt));
            builder.Append("</details>");
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }

    private static string CheckPanel(RunReportCheck check, int truncateOutputAt)
    {
        var builder = new StringBuilder();
        builder.Append("<section class=\"panel\"><strong>");
        builder.Append(Encode(check.Id));
        builder.Append("</strong> <span class=\"muted\">");
        builder.Append(Encode(check.Outcome));
        builder.Append("</span>");
        if (!string.IsNullOrEmpty(check.MissingToolchain))
        {
            builder.Append("<p class=\"warn\">");
            builder.Append(Encode(check.MissingToolchain));
            builder.Append("</p>");
        }
        else if (!string.IsNullOrEmpty(check.SkipReason))
        {
            builder.Append("<p class=\"warn\">");
            builder.Append(Encode(check.SkipReason));
            builder.Append("</p>");
        }

        builder.Append("<p class=\"muted\">");
        builder.Append(Encode(check.Command));
        builder.Append("</p>");
        if (!string.IsNullOrEmpty(check.Output))
        {
            builder.Append("<pre>");
            builder.Append(Encode(Truncate(check.Output, truncateOutputAt)));
            builder.Append("</pre>");
        }

        builder.Append("</section>");
        return builder.ToString();
    }

    private static string Truncate(string value, int limit)
        => value.Length <= limit ? value : value[..limit] + "…";

    private static string Encode(string value)
        => WebUtility.HtmlEncode(value);
}
