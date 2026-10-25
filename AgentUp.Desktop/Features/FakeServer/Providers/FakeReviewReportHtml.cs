using System.Text;
using System.Text.Json.Nodes;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

public static class FakeReviewReportHtml
{
    public static string Render(string workspaceId, JsonObject workspace)
    {
        var name = workspace["displayName"]?.GetValue<string>() ?? workspaceId;
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"en\" class=\"au-theme\"><head><meta charset=\"utf-8\"><title>");
        builder.Append(System.Net.WebUtility.HtmlEncode($"Export Report — {name}"));
        builder.Append("</title></head><body class=\"au-theme\"><article class=\"au-overview au-report\">");
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
}
