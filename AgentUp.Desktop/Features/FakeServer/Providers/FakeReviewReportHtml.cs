using System.Text;
using System.Text.Json.Nodes;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

public static class FakeReviewReportHtml
{
    internal const string CssResourceName = "AgentUp.DesignSystem.agent-up.css";

    private const string ReportCss = """
        .au-report { width: min(100% - 2rem, var(--au-content-width)); margin: var(--au-space-8) auto var(--au-space-16); }
        .au-report h2 { margin: var(--au-space-8) 0 var(--au-space-3); font-size: var(--au-font-size-lg); font-weight: var(--au-weight-bold); }
        .au-report-shot { display: block; max-width: 100%; margin-top: var(--au-space-3); border-radius: var(--au-radius-lg); border: 1px solid var(--au-color-border-subtle); }
        .au-report pre.au-code { white-space: pre-wrap; word-break: break-word; max-height: 24rem; overflow: auto; }
        .au-report-row { display: grid; grid-template-columns: 9rem 1fr; gap: var(--au-space-3); padding: var(--au-space-2) 0; border-bottom: 1px solid var(--au-color-border-subtle); }
        .au-report-row dt { margin: 0; color: var(--au-color-text-muted); font-size: var(--au-font-size-ui-sm); }
        .au-report-row dd { margin: 0; }
        """;

    public static string Render(string workspaceId, JsonObject workspace)
    {
        var name = workspace["displayName"]?.GetValue<string>() ?? workspaceId;
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"en\" class=\"au-theme\"><head><meta charset=\"utf-8\">");
        builder.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("<title>");
        builder.Append(System.Net.WebUtility.HtmlEncode($"Export Report — {name}"));
        builder.Append("</title><style>");
        builder.Append(LoadCss());
        builder.Append(ReportCss);
        builder.Append("</style></head><body class=\"au-theme\"><article class=\"au-overview au-report\">");
        builder.Append("<p class=\"au-eyebrow\">Export Report</p><h1 class=\"au-page-title\">");
        builder.Append(System.Net.WebUtility.HtmlEncode(name));
        builder.Append("</h1><p class=\"au-muted\">");
        builder.Append(System.Net.WebUtility.HtmlEncode(workspace["state"]?.GetValue<string>() ?? ""));
        builder.Append("</p>");
        builder.Append("<p class=\"au-mono\">");
        builder.Append(System.Net.WebUtility.HtmlEncode(workspace["repositoryPath"]?.GetValue<string>() ?? ""));
        builder.Append("</p><h2>Applications</h2>");
        foreach (var app in (workspace["applications"] as JsonArray ?? []).OfType<JsonObject>())
        {
            builder.Append("<article class=\"au-card\"><h3>");
            builder.Append(System.Net.WebUtility.HtmlEncode(app["name"]?.GetValue<string>() ?? ""));
            builder.Append("</h3><p class=\"au-muted\">");
            builder.Append(System.Net.WebUtility.HtmlEncode(app["kind"]?.GetValue<string>() ?? "Process"));
            builder.Append(" · ");
            builder.Append(System.Net.WebUtility.HtmlEncode(app["state"]?.GetValue<string>() ?? ""));
            builder.Append("</p>");
            foreach (var port in (app["allocatedPorts"] as JsonArray ?? []).OfType<JsonObject>())
            {
                builder.Append("<p class=\"au-mono\">");
                builder.Append(System.Net.WebUtility.HtmlEncode(port["variable"]?.GetValue<string>() ?? "port"));
                builder.Append(" = ");
                builder.Append(port["allocatedPort"]?.GetValue<int>() ?? 0);
                builder.Append(" (");
                builder.Append(System.Net.WebUtility.HtmlEncode(port["protocol"]?.GetValue<string>() ?? "http"));
                builder.Append(")</p>");
            }

            builder.Append("</article>");
        }

        builder.Append("<h2>Screenshots</h2><p class=\"au-muted\">No screenshots.</p>");
        builder.Append("<h2>Verification</h2><p class=\"au-muted\">Verification could not run for this worktree.</p>");
        builder.Append("</article></body></html>");
        return builder.ToString();
    }

    internal static string LoadCss()
    {
        var assembly = typeof(FakeReviewReportHtml).Assembly;
        using var stream = assembly.GetManifestResourceStream(CssResourceName)
            ?? throw new InvalidOperationException("The compiled design-system stylesheet is missing from Desktop Demo.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
