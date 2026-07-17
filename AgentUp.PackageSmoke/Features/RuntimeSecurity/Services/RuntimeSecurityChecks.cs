using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using System.Net;
using System.Text.RegularExpressions;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Providers;
using AgentUp.PackageSmoke.Features.PackageValidation.Services;

namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Services;

public sealed class RuntimeSecurityChecks : IRuntimeSecurityChecks
{
    private static readonly Regex VersionPattern = new(@"\d+\.\d+", RegexOptions.Compiled);

    private readonly INetworkStateProvider _networkState;
    private readonly HttpClient _httpClient;

    public RuntimeSecurityChecks(INetworkStateProvider networkState, HttpClient httpClient)
    {
        _networkState = networkState;
        _httpClient = httpClient;
    }

    public async Task RunAsync(string serverUrl, FileAssertions assert, CancellationToken cancellationToken = default)
    {
        CheckPortBinding(serverUrl, assert);
        await CheckResponseHeadersAsync(serverUrl, assert, cancellationToken);
    }

    private void CheckPortBinding(string serverUrl, FileAssertions assert)
    {
        var port = new Uri(serverUrl).Port;
        var onPort = _networkState.GetActiveTcpListeners()
            .Where(ep => ep.Port == port)
            .ToList();

        if (onPort.Count == 0)
        {
            assert.Info("security.binding.loopback", $"No TCP listeners found on port {port}.");
            return;
        }

        var nonLoopback = onPort
            .Where(ep => !IPAddress.IsLoopback(ep.Address))
            .ToList();

        if (nonLoopback.Count > 0)
            assert.Error("security.binding.loopback",
                $"Server port {port} is bound to non-loopback address(es): {string.Join(", ", nonLoopback.Select(ep => ep.Address.ToString()))}");
        else
            assert.Info("security.binding.loopback", $"Port {port} is bound to loopback only.");
    }

    private async Task CheckResponseHeadersAsync(string serverUrl, FileAssertions assert, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{serverUrl.TrimEnd('/')}/api/workspaces",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var serverHeader = response.Headers.Server.ToString();

            if (VersionPattern.IsMatch(serverHeader))
                assert.Error("security.headers.server",
                    $"Server header exposes version information: \"{serverHeader}\"");
            else
                assert.Info("security.headers.server",
                    $"Server header is non-disclosing: \"{serverHeader}\"");
        }
        catch (HttpRequestException ex)
        {
            assert.Error("security.headers.probe",
                $"Could not reach server to check response headers: {ex.Message}");
        }
    }
}
