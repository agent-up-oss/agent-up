using System.Reflection;

namespace AgentUp.Server.Features.Browser.Resources;

internal static class RdpViewerPage
{
    private const string JsResourceName = "AgentUp.Server.Features.Browser.Resources.rdp-viewer.js";
    private static readonly Lazy<string> ViewerJs = new(LoadEmbeddedJs);

    // The JS is embedded and self-contained (reads workspaceId from URL query, exposes
    // window.__viewer state-machine API). This wrapper produces the HTML shell that
    // hosts it. WorkspaceId is intentionally NOT interpolated into the JS: the viewer
    // reads it from location.search so the same JS bundle works for any workspace and
    // the resource load is cached across pages.
    public static string Build(string workspaceId)
    {
        _ = workspaceId; // signature kept for callers; value is passed via query string.
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AgentUp Browser</title>
              <style>
                html, body { margin: 0; padding: 0; background: #1e1e1e; overflow: hidden; width: 100%; height: 100%; }
                #c { display: block; width: 100%; height: 100%; object-fit: contain; }
                #ai-badge {
                  display: block; position: fixed; bottom: 8px; right: 10px;
                  background: #222; color: #888; font: bold 11px/1 monospace;
                  padding: 3px 7px; border-radius: 4px; border: 1px solid #444;
                  pointer-events: none; z-index: 9;
                }
              </style>
            </head>
            <body>
            <canvas id="c"></canvas>
            <div id="ai-badge">AI</div>
            <script>
            {{ViewerJs.Value}}
            </script>
            </body>
            </html>
            """;
    }

    private static string LoadEmbeddedJs()
    {
        var assembly = typeof(RdpViewerPage).Assembly;
        using var stream = assembly.GetManifestResourceStream(JsResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{JsResourceName}' not found. Check the <EmbeddedResource> entry in AgentUp.Server.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
