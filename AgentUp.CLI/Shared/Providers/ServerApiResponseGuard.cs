using System.Net;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Shared.Providers;

public static class ServerApiResponseGuard
{
    public static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new AuthenticationRequiredException();
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        EnsureSuccess(response);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? body;
            return body;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            return $"HTTP {(int)response.StatusCode}";
        }
    }
}
