using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// The Server's Files routes over a small in-memory checkout, for headless tests that open the
/// Files tab. It records which folders were asked for, so a test can prove nothing was fetched
/// ahead of the reader.
/// </summary>
internal sealed class FakeWorkspaceFiles
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal)
    {
        [DesktopDomain.FileReadme] = "# Harbor Shop\n",
        [DesktopDomain.FileProductGrid] = "export function ProductGrid() {}\n",
        ["apps/api/orders.ts"] = "export const openOrders = 12;\n",
    };

    public Dictionary<string, string> Statuses { get; } = new(StringComparer.Ordinal)
    {
        [DesktopDomain.FileProductGrid] = "Modified",
    };

    public List<string> ListedFolders { get; } = [];

    public int Saves { get; private set; }

    /// <summary>When set, the next save is refused as if an agent had changed the file first.</summary>
    public bool RefuseNextSaveAsStale { get; set; }

    public static bool IsFilesRoute(string path)
        => path.StartsWith("/api/workspaces/", StringComparison.Ordinal) && path.Contains("/files/", StringComparison.Ordinal);

    public async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, string path)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri?.Query ?? "");
        if (path.EndsWith("/files/directory", StringComparison.Ordinal))
            return Directory(query["path"] ?? "");
        if (request.Method == HttpMethod.Get && path.EndsWith("/files/file", StringComparison.Ordinal))
            return File(query["path"] ?? "");
        if (request.Method == HttpMethod.Put && path.EndsWith("/files/file", StringComparison.Ordinal))
            return Save(await request.Content!.ReadAsStringAsync());
        return Respond(HttpStatusCode.NotFound, new { title = "Not found" });
    }

    public static string Version(string text) => $"v{text.GetHashCode(StringComparison.Ordinal):x8}";

    private HttpResponseMessage Directory(string path)
    {
        ListedFolders.Add(path);
        var prefix = path.Length == 0 ? "" : path + "/";
        var under = Files.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Select(key => key[prefix.Length..]).ToList();
        var folders = under.Where(rest => rest.Contains('/')).Select(rest => rest[..rest.IndexOf('/')]).Distinct().Order(StringComparer.Ordinal)
            .Select(name => new
            {
                name,
                path = prefix + name,
                kind = "Directory",
                size = (long?)null,
                status = (string?)null,
                changedFiles = Statuses.Keys.Count(key => key.StartsWith(prefix + name + "/", StringComparison.Ordinal)),
            });
        var files = under.Where(rest => !rest.Contains('/')).Order(StringComparer.Ordinal)
            .Select(name => new
            {
                name,
                path = prefix + name,
                kind = "File",
                size = (long?)Files[prefix + name].Length,
                status = Statuses.GetValueOrDefault(prefix + name),
                changedFiles = 0,
            });
        return Respond(HttpStatusCode.OK, new { workspaceId = "ws", path, entries = folders.Concat(files).ToArray(), truncated = false });
    }

    private HttpResponseMessage File(string path)
        => Files.TryGetValue(path, out var text)
            ? Respond(HttpStatusCode.OK, Content(path, text))
            : Respond(HttpStatusCode.NotFound, new { title = "Not found", detail = $"'{path}' is not a file in this workspace." });

    private HttpResponseMessage Save(string json)
    {
        var body = JsonNode.Parse(json)!;
        var path = body["path"]!.GetValue<string>();
        var content = body["content"]!.GetValue<string>();
        if (RefuseNextSaveAsStale || body["version"]?.GetValue<string>() != Version(Files[path]))
        {
            RefuseNextSaveAsStale = false;
            return Respond(HttpStatusCode.Conflict, new { title = "File changed on the Server", detail = $"'{path}' changed on the Server since it was opened. Reload it to see the change." });
        }

        Saves++;
        Files[path] = content;
        Statuses.TryAdd(path, "Modified");
        return Respond(HttpStatusCode.OK, Content(path, content));
    }

    private object Content(string path, string text)
        => new { path, size = (long)text.Length, version = Version(text), status = Statuses.GetValueOrDefault(path), isBinary = false, isTooLarge = false, content = text };

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage Respond<T>(HttpStatusCode status, T value) => new(status) { Content = JsonContent.Create(value) };
}
