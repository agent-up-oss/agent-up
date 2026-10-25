using System.Globalization;
using System.Net;
using System.Text;
using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Diagnostics.Models;
using AgentUp.Server.Features.Verification.DTOs;

namespace AgentUp.Server.Features.Diagnostics.Providers;

public sealed class ReviewSnapshotHtmlProvider(ReviewSnapshotCssProvider css)
{
    private const string ReportCss = """
        .au-report { width: min(100% - 2rem, var(--au-content-width)); margin: var(--au-space-8) auto var(--au-space-16); }
        .au-report h2 { margin: var(--au-space-8) 0 var(--au-space-3); font-size: var(--au-font-size-lg); font-weight: var(--au-weight-bold); }
        .au-report-shot { display: block; max-width: 100%; margin-top: var(--au-space-3); border-radius: var(--au-radius-lg); border: 1px solid var(--au-color-border-subtle); }
        .au-report pre.au-code { white-space: pre-wrap; word-break: break-word; max-height: 24rem; overflow: auto; }
        .au-report-row { display: grid; grid-template-columns: 9rem 1fr; gap: var(--au-space-3); padding: var(--au-space-2) 0; border-bottom: 1px solid var(--au-color-border-subtle); }
        .au-report-row dt { margin: 0; color: var(--au-color-text-muted); font-size: var(--au-font-size-ui-sm); }
        .au-report-row dd { margin: 0; }
        """;

    public string Render(WorkspaceReviewSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"en\" class=\"au-theme\"><head><meta charset=\"utf-8\">");
        builder.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("<title>");
        builder.Append(Encode($"Export Report — {snapshot.DisplayName}"));
        builder.Append("</title><style>");
        builder.Append(css.Load());
        builder.Append(ReportCss);
        builder.Append("</style></head><body class=\"au-theme\"><article class=\"au-overview au-report\">");
        Header(builder, snapshot);
        Metrics(builder, snapshot);
        Identity(builder, snapshot);
        Applications(builder, snapshot);
        Diagnostics(builder, snapshot);
        Actions(builder, snapshot);
        Screenshots(builder, snapshot);
        Verification(builder, snapshot.Verification);
        builder.Append("</article></body></html>");
        return builder.ToString();
    }

    private static void Header(StringBuilder builder, WorkspaceReviewSnapshot snapshot)
    {
        builder.Append("<header class=\"au-stack\"><p class=\"au-eyebrow\">Export Report</p><h1 class=\"au-page-title\">");
        builder.Append(Encode(snapshot.DisplayName));
        builder.Append("</h1><p class=\"au-muted\">Captured ");
        builder.Append(Encode(snapshot.CapturedAt.ToString("u", CultureInfo.InvariantCulture)));
        builder.Append(". Evidence to read, never a substitute for the live workspace, and never an input to verify guard.</p></header>");
    }

    private static void Metrics(StringBuilder builder, WorkspaceReviewSnapshot snapshot)
    {
        builder.Append("<div class=\"au-overview-grid\">");
        Metric(builder, "State", snapshot.State, StateClass(snapshot.State));
        Metric(builder, "Health", snapshot.Health ?? "—", HealthClass(snapshot.Health));
        Metric(builder, "CPU", snapshot.CpuPercent is { } cpu ? $"{cpu:0.0}%" : "—", null);
        Metric(builder, "Memory", snapshot.MemoryBytes is { } memory ? FormatBytes(memory) : "—", null);
        Metric(builder, "Storage", snapshot.StorageBytes is { } storage ? FormatBytes(storage) : "—", null);
        Metric(builder, "Processes", snapshot.ProcessCount?.ToString(CultureInfo.InvariantCulture) ?? "—", null);
        Metric(builder, "Applications", snapshot.Applications.Count.ToString(CultureInfo.InvariantCulture), null);
        builder.Append("</div>");
    }

    private static void Metric(StringBuilder builder, string label, string value, string? valueClass)
    {
        builder.Append("<div class=\"au-card\"><span class=\"au-field-label\">");
        builder.Append(Encode(label));
        builder.Append("</span><strong");
        if (valueClass is not null)
        {
            builder.Append(" class=\"");
            builder.Append(valueClass);
            builder.Append('"');
        }

        builder.Append('>');
        builder.Append(Encode(value));
        builder.Append("</strong></div>");
    }

    private static void Identity(StringBuilder builder, WorkspaceReviewSnapshot snapshot)
    {
        builder.Append("<section class=\"au-stack\"><h2>Workspace</h2>");
        Fact(builder, "Repository", snapshot.RepositoryPath, mono: true);
        Fact(builder, "Worktree", snapshot.WorktreePath, mono: true);
        Fact(builder, "Branch", string.IsNullOrWhiteSpace(snapshot.Branch) ? "not on a git branch" : snapshot.Branch, mono: true);
        Fact(builder, "Commit", string.IsNullOrWhiteSpace(snapshot.Commit) ? "—" : snapshot.Commit, mono: true);
        Fact(builder, "Browser URL", snapshot.BrowserUrl ?? "No open browser URL.", mono: snapshot.BrowserUrl is not null);
        builder.Append("</section>");
    }

    private static void Applications(StringBuilder builder, WorkspaceReviewSnapshot snapshot)
    {
        builder.Append("<section><h2>Applications</h2>");
        if (snapshot.Applications.Count == 0)
        {
            builder.Append("<p class=\"au-muted\">No applications are registered.</p></section>");
            return;
        }

        foreach (var app in snapshot.Applications)
        {
            builder.Append("<article class=\"au-card au-stack\" style=\"margin-bottom: var(--au-space-3)\"><h3 class=\"au-page-title\">");
            builder.Append(Encode(app.Name));
            builder.Append("</h3><p><span class=\"au-badge ");
            builder.Append(StateBadge(app.ProcessState));
            builder.Append("\">");
            builder.Append(Encode(app.ProcessState));
            builder.Append("</span> <span class=\"au-muted\">");
            builder.Append(Encode(app.Kind));
            builder.Append("</span></p>");
            Fact(builder, "Health", app.Health ?? "—");
            Fact(builder, "Readiness", app.Readiness);
            builder.Append("<p class=\"au-field-label\">Ports</p>");
            if (app.Ports.Count == 0)
                builder.Append("<p class=\"au-muted\">No allocated ports.</p>");
            else
            {
                foreach (var port in app.Ports)
                {
                    builder.Append("<p class=\"au-mono\">");
                    builder.Append(Encode(port.Variable ?? "port"));
                    builder.Append(" = ");
                    builder.Append(port.AllocatedPort.ToString(CultureInfo.InvariantCulture));
                    builder.Append(" (");
                    builder.Append(Encode(port.Protocol));
                    builder.Append(")</p>");
                }
            }

            builder.Append("<p class=\"au-field-label\">Logs</p>");
            if (app.Logs.Count == 0)
                builder.Append("<p class=\"au-muted\">No captured logs.</p>");
            else
            {
                builder.Append("<details");
                if (app.Logs.Count <= 12)
                    builder.Append(" open");
                builder.Append("><summary class=\"au-muted\">");
                builder.Append(app.Logs.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(" line");
                if (app.Logs.Count != 1)
                    builder.Append('s');
                if (app.LogsTruncated)
                    builder.Append(" (truncated)");
                builder.Append("</summary><pre class=\"au-code\">");
                builder.Append(Encode(string.Join('\n', app.Logs)));
                builder.Append("</pre></details>");
            }

            builder.Append("</article>");
        }

        builder.Append("</section>");
    }

    private static void Diagnostics(StringBuilder builder, WorkspaceReviewSnapshot snapshot)
    {
        builder.Append("<section><h2>Diagnostics</h2>");
        if (snapshot.Entries.Count == 0)
        {
            builder.Append("<p class=\"au-muted\">No diagnostic entries.</p></section>");
            return;
        }

        foreach (var entry in snapshot.Entries)
        {
            builder.Append("<article class=\"au-card\" style=\"margin-bottom: var(--au-space-2)\"><p><span class=\"au-badge ");
            builder.Append(SeverityBadge(entry.Severity, entry.State));
            builder.Append("\">");
            builder.Append(Encode(entry.Severity));
            builder.Append("</span> <span class=\"au-muted\">");
            builder.Append(Encode(entry.Category));
            builder.Append(" · ");
            builder.Append(Encode(entry.State));
            builder.Append("</span></p><p>");
            builder.Append(Encode(entry.Message));
            builder.Append("</p><p class=\"au-muted\">");
            builder.Append(Encode(entry.Timestamp.ToString("u", CultureInfo.InvariantCulture)));
            if (!string.IsNullOrWhiteSpace(entry.Application))
            {
                builder.Append(" · ");
                builder.Append(Encode(entry.Application));
            }

            builder.Append(" · ");
            builder.Append(Encode(entry.Action));
            builder.Append("</p></article>");
        }

        builder.Append("</section>");
    }

    private static void Actions(StringBuilder builder, WorkspaceReviewSnapshot snapshot)
    {
        builder.Append("<section><h2>Recent actions</h2>");
        if (snapshot.RecentActions.Count == 0)
        {
            builder.Append("<p class=\"au-muted\">No recent workspace or MCP actions.</p></section>");
            return;
        }

        foreach (var action in snapshot.RecentActions)
        {
            builder.Append("<article class=\"au-card\" style=\"margin-bottom: var(--au-space-2)\"><p><strong>");
            builder.Append(Encode(action.Action));
            builder.Append("</strong> <span class=\"au-badge ");
            builder.Append(OutcomeBadge(action.Outcome));
            builder.Append("\">");
            builder.Append(Encode(action.Outcome));
            builder.Append("</span></p><p class=\"au-muted\">");
            builder.Append(Encode(action.Timestamp.ToString("u", CultureInfo.InvariantCulture)));
            builder.Append(" · ");
            builder.Append(Encode(action.Kind));
            builder.Append(" · ");
            builder.Append(Encode(action.Source));
            builder.Append("</p>");
            if (!string.Equals(action.Detail, action.Action, StringComparison.Ordinal))
            {
                builder.Append("<p>");
                builder.Append(Encode(action.Detail));
                builder.Append("</p>");
            }

            builder.Append("</article>");
        }

        builder.Append("</section>");
    }

    private static void Screenshots(StringBuilder builder, WorkspaceReviewSnapshot snapshot)
    {
        builder.Append("<section><h2>Screenshots</h2>");
        if (snapshot.Screenshots.Count == 0)
        {
            builder.Append("<p class=\"au-muted\">No screenshots.</p></section>");
            return;
        }

        foreach (var shot in snapshot.Screenshots)
        {
            builder.Append("<figure class=\"au-card\" style=\"margin: 0 0 var(--au-space-3)\"><figcaption class=\"au-muted\">");
            builder.Append(Encode(shot.Timestamp.ToString("u", CultureInfo.InvariantCulture)));
            if (!string.IsNullOrWhiteSpace(shot.Url))
            {
                builder.Append(" · ");
                builder.Append(Encode(shot.Url));
            }

            builder.Append("</figcaption>");
            if (!string.IsNullOrWhiteSpace(shot.ImageBase64) && !string.IsNullOrWhiteSpace(shot.MimeType))
            {
                builder.Append("<img class=\"au-report-shot\" alt=\"");
                builder.Append(Encode(shot.Url ?? "Workspace screenshot"));
                builder.Append("\" src=\"data:");
                builder.Append(Encode(shot.MimeType));
                builder.Append(";base64,");
                builder.Append(shot.ImageBase64);
                builder.Append("\">");
            }
            else
            {
                builder.Append("<p class=\"au-muted\">Screenshot artifact ");
                builder.Append(Encode(shot.ArtifactId));
                builder.Append(" has no embedded image bytes.</p>");
            }

            builder.Append("</figure>");
        }

        builder.Append("</section>");
    }

    private static void Verification(StringBuilder builder, VerificationExportDto? verification)
    {
        builder.Append("<section><h2>Verification</h2>");
        if (verification is null)
        {
            builder.Append("<p class=\"au-muted\">Verification could not run for this worktree. Missing verification does not fail the report.</p></section>");
            return;
        }

        var passed = verification.Checks.Count(check => StatusIs(check.Status, "passed"));
        var failed = verification.Checks.Count(check => StatusIs(check.Status, "failed"));
        var skipped = verification.Checks.Count(check => StatusIs(check.Status, "skipped"));
        var neverRun = verification.Checks.Count(check => StatusIs(check.Status, "neverRun"));
        builder.Append("<p>");
        builder.Append(Encode($"{passed} passed, {failed} failed, {skipped} skipped, {neverRun} never run. {verification.ChangedFileCount} changed file(s)."));
        builder.Append("</p>");
        Fact(builder, "Revision", verification.Revision, mono: true);
        Fact(builder, "Digest", verification.CoveredDigest, mono: true);
        Fact(builder, "Exported", verification.ExportedAtUtc, mono: true);
        if (verification.UnmatchedFiles.Count > 0)
        {
            builder.Append("<p class=\"au-field-label\">Unmatched files</p><ul>");
            foreach (var file in verification.UnmatchedFiles)
            {
                builder.Append("<li class=\"au-mono\">");
                builder.Append(Encode(file));
                builder.Append("</li>");
            }

            builder.Append("</ul>");
        }

        foreach (var check in verification.Checks)
        {
            builder.Append("<article class=\"au-card\" style=\"margin-bottom: var(--au-space-2)\"><p><strong>");
            builder.Append(Encode(check.CheckId));
            builder.Append("</strong> <span class=\"au-badge ");
            builder.Append(CheckBadge(check.Status));
            builder.Append("\">");
            builder.Append(Encode(check.Status));
            builder.Append("</span></p><p class=\"au-mono\">");
            builder.Append(Encode(check.Command));
            builder.Append("</p>");
            if (check.ExitCode is { } exit)
            {
                builder.Append("<p class=\"au-muted\">Exit ");
                builder.Append(exit.ToString(CultureInfo.InvariantCulture));
                builder.Append("</p>");
            }

            if (!string.IsNullOrWhiteSpace(check.SkipReason))
            {
                builder.Append("<p class=\"au-muted\">");
                builder.Append(Encode(check.SkipReason));
                builder.Append("</p>");
            }

            if (!string.IsNullOrWhiteSpace(check.Detail))
            {
                builder.Append("<p>");
                builder.Append(Encode(check.Detail));
                builder.Append("</p>");
            }

            builder.Append("</article>");
        }

        builder.Append("</section>");
    }

    private static void Fact(StringBuilder builder, string label, string value, bool mono = false)
    {
        builder.Append("<div><span class=\"au-field-label\">");
        builder.Append(Encode(label));
        builder.Append("</span><p");
        if (mono)
            builder.Append(" class=\"au-mono\"");
        builder.Append('>');
        builder.Append(Encode(value));
        builder.Append("</p></div>");
    }

    private static string StateClass(string state)
        => state.Equals("Running", StringComparison.OrdinalIgnoreCase) ? "au-accent" : "au-muted";

    private static string HealthClass(string? health)
        => health is not null && health.Equals("Healthy", StringComparison.OrdinalIgnoreCase) ? "au-accent" : "au-muted";

    private static string StateBadge(string state)
        => state.Equals("Running", StringComparison.OrdinalIgnoreCase)
            ? "au-badge--healthy"
            : state.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                ? "au-badge--danger"
                : "au-badge--info";

    private static string SeverityBadge(string severity, string state)
        => state.Equals("resolved", StringComparison.OrdinalIgnoreCase)
            ? "au-badge--info"
            : severity.Equals("error", StringComparison.OrdinalIgnoreCase)
                ? "au-badge--danger"
                : severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
                    ? "au-badge--warning"
                    : "au-badge--info";

    private static string OutcomeBadge(string outcome)
        => outcome.Equals("success", StringComparison.OrdinalIgnoreCase)
            || outcome.Equals("Running", StringComparison.OrdinalIgnoreCase)
            || outcome.Equals("healthy", StringComparison.OrdinalIgnoreCase)
            ? "au-badge--healthy"
            : outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                || outcome.Equals("failure", StringComparison.OrdinalIgnoreCase)
                ? "au-badge--danger"
                : "au-badge--info";

    private static string CheckBadge(string status)
        => StatusIs(status, "passed")
            ? "au-badge--healthy"
            : StatusIs(status, "failed")
                ? "au-badge--danger"
                : "au-badge--warning";

    private static bool StatusIs(string status, string expected)
        => status.Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
