using System.Net;
using System.Text.RegularExpressions;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Providers;

namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;

public sealed class RuntimeSecurityChecks : IRuntimeSecurityChecks, IDisposable
{
    private static readonly Regex VersionPattern = new(@"\d+\.\d+", RegexOptions.Compiled);

    private readonly INetworkStateProvider _networkState;
    private readonly HttpClient _httpClient;

    public RuntimeSecurityChecks(INetworkStateProvider networkState, HttpClient httpClient)
    {
        _networkState = networkState;
        _httpClient = httpClient;
    }

    public async Task RunAsync(string serverUrl, IRuntimeSecurityFindingSink findings, CancellationToken cancellationToken = default)
    {
        CheckPortBinding(serverUrl, findings);
        await CheckResponseHeadersAsync(serverUrl, findings, cancellationToken);
    }

    private void CheckPortBinding(string serverUrl, IRuntimeSecurityFindingSink findings)
    {
        var port = new Uri(serverUrl).Port;
        var onPort = _networkState.GetActiveTcpListeners()
            .Where(ep => ep.Port == port)
            .ToList();

        if (onPort.Count == 0)
        {
            findings.Info("security.binding.loopback", $"No TCP listeners found on port {port}.");
            return;
        }

        var nonLoopback = onPort
            .Where(ep => !IPAddress.IsLoopback(ep.Address))
            .ToList();

        if (nonLoopback.Count > 0)
            findings.Error("security.binding.loopback",
                $"Server port {port} is bound to non-loopback address(es): {string.Join(", ", nonLoopback.Select(ep => ep.Address))}");
        else
            findings.Info("security.binding.loopback", $"Port {port} is bound to loopback only.");
    }

    private async Task CheckResponseHeadersAsync(string serverUrl, IRuntimeSecurityFindingSink findings, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{serverUrl.TrimEnd('/')}/api/workspaces",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var serverHeader = response.Headers.Server.ToString();

            if (VersionPattern.IsMatch(serverHeader))
                findings.Error("security.headers.server",
                    $"Server header exposes version information: \"{serverHeader}\"");
            else
                findings.Info("security.headers.server",
                    $"Server header is non-disclosing: \"{serverHeader}\"");
        }
        catch (HttpRequestException ex)
        {
            findings.Error("security.headers.probe",
                $"Could not reach server to check response headers: {ex.Message}");
        }
    }

    public void Dispose()
        => _httpClient.Dispose();
}
