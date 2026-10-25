using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class GithubAppApiProvider(HttpMessageHandler? handler = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsOrganizationAsync(
        string apiBaseUrl, string owner, CancellationToken cancellationToken)
    {
        using var client = Client(apiBaseUrl);
        using var response = await client.GetAsync($"users/{Uri.EscapeDataString(owner)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        return value.TryGetProperty("type", out var type)
               && type.GetString()?.Equals("Organization", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<GithubAppConnection> ConvertManifestAsync(
        GithubAppConnection pending, string code, CancellationToken cancellationToken)
    {
        using var client = Client(pending.ApiBaseUrl);
        using var response = await client.PostAsync(
            $"app-manifests/{Uri.EscapeDataString(code)}/conversions", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        return pending with
        {
            AppId = value.GetProperty("id").GetInt64(),
            Slug = value.GetProperty("slug").GetString(),
            PrivateKey = value.GetProperty("pem").GetString()
        };
    }

    public async Task<bool> VerifyInstallationAsync(
        GithubAppConnection connection, long installationId, CancellationToken cancellationToken)
    {
        using var client = AppClient(connection);
        using var response = await client.GetAsync($"app/installations/{installationId}", cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        var value = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        return string.IsNullOrWhiteSpace(connection.RepositoryOwner)
               || value.TryGetProperty("account", out var account)
               && account.TryGetProperty("login", out var login)
               && login.GetString()?.Equals(connection.RepositoryOwner, StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<string> CreateInstallationTokenAsync(
        GithubAppConnection connection, string repository, CancellationToken cancellationToken)
    {
        if (connection.InstallationId is null)
            throw new InvalidOperationException("The workspace GitHub App is not installed.");
        using var client = AppClient(connection);
        using var response = await client.PostAsJsonAsync(
            $"app/installations/{connection.InstallationId}/access_tokens",
            new { repositories = new[] { repository } }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);
        return value.GetProperty("token").GetString()
               ?? throw new InvalidOperationException("GitHub did not return an installation token.");
    }

    private HttpClient AppClient(GithubAppConnection connection)
    {
        if (connection.AppId is null || string.IsNullOrWhiteSpace(connection.PrivateKey))
            throw new InvalidOperationException("The workspace GitHub App registration is incomplete.");
        var client = Client(connection.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", Jwt(connection.AppId.Value, connection.PrivateKey));
        return client;
    }

    private HttpClient Client(string apiBaseUrl)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = new Uri(apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("agent-up");
        return client;
    }

    private static string Jwt(long appId, string privateKey)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            iat = now - 60,
            exp = now + 540,
            iss = appId
        })));
        var unsigned = header + "." + payload;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
