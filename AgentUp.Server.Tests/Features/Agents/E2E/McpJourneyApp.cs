namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>A workspace whose one application serves a page with a button that changes its heading.</summary>
internal sealed class McpJourneyApp : IDisposable
{
    private const string Server = """
        import http.server, os
        PAGE = b'<!doctype html><title>Harbor</title><h1 id="title">Harbor</h1><button id="go" onclick="document.getElementById(\'title\').textContent=\'Clicked\'">Go</button>'
        class Page(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(200)
                self.send_header('content-type', 'text/html')
                self.end_headers()
                self.wfile.write(PAGE)
            def log_message(self, *args):
                pass
        http.server.HTTPServer(('127.0.0.1', int(os.environ['WEB_PORT'])), Page).serve_forever()
        """;

    // The script is named by absolute path: the Server launches a plain command from its own
    // directory and passes the workspace only to tools it knows, which Python is not.
    private const string Configuration = """
        {
          "name": "MCP journey",
          "applications": [
            {
              "name": "Web",
              "command": "python3 APP",
              "ports": [ { "variable": "WEB_PORT", "defaultPort": 5800, "protocol": "http" } ]
            }
          ]
        }
        """;

    public McpJourneyApp()
    {
        Path = Directory.CreateTempSubdirectory("agent-up-mcp-journey").FullName;
        File.WriteAllText(System.IO.Path.Join(Path, "app.py"), Server);
        File.WriteAllText(System.IO.Path.Join(Path, "agent-up.json"),
            Configuration.Replace("APP", System.IO.Path.Join(Path, "app.py"), StringComparison.Ordinal));
        File.WriteAllText(System.IO.Path.Join(Path, "README.md"), "mcp journey\n");
    }

    public string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TestContext.Out.WriteLine($"Could not remove {Path}: {exception.Message}");
        }
    }
}
