using System.Text.RegularExpressions;

namespace AgentUp.Server.Features.Applications.Providers;

public sealed partial class AppMetricsHttpClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<IReadOnlyDictionary<string, string>?> FetchAsync(
        int allocatedPort,
        string path,
        CancellationToken cancellationToken)
    {
        if (!TryBuildUri(allocatedPort, path, out var uri))
            return null;

        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return MetricsResponseParser.Parse(body);
    }

    // Metrics paths come from workspace-supplied configuration (agent-up.json). Building the
    // request URI by string interpolation would let a value like "@attacker.example/" redirect
    // the request to a different host, so the path is validated as origin-relative and the URI
    // is assembled component-wise instead.
    private static bool TryBuildUri(int allocatedPort, string path, out Uri uri)
    {
        uri = null!;
        if (!SafeMetricsPath().IsMatch(path) || path.Contains("//", StringComparison.Ordinal))
            return false;

        uri = new UriBuilder(Uri.UriSchemeHttp, "localhost", allocatedPort) { Path = path }.Uri;
        return true;
    }

    [GeneratedRegex(@"^/[A-Za-z0-9._~\-/]*$")]
    private static partial Regex SafeMetricsPath();

    public void Dispose() => _http.Dispose();
}
