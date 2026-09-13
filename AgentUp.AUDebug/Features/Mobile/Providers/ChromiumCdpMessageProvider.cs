using System.Text.Json;

namespace AgentUp.AUDebug.Features.Mobile.Providers;

public static class ChromiumCdpMessageProvider
{
    public static string Evaluate(string expression, int id = 1)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["method"] = "Runtime.evaluate",
            ["params"] = new Dictionary<string, object?>
            {
                ["expression"] = expression,
                ["awaitPromise"] = true,
                ["returnByValue"] = true
            }
        });

    public static string CaptureScreenshot(int id = 2)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["method"] = "Page.captureScreenshot",
            ["params"] = new Dictionary<string, object?>
            {
                ["format"] = "png",
                ["fromSurface"] = true
            }
        });

    public static void ThrowIfEvaluateFailed(string json, string action)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException($"{action} CDP failed: {error}");
        if (document.RootElement.TryGetProperty("result", out var body)
            && body.TryGetProperty("exceptionDetails", out var details))
            throw new InvalidOperationException($"{action} failed: {details}");
    }

    public static byte[] ReadPng(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException($"Mobile open-agent screenshot failed: {error}");
        if (!document.RootElement.TryGetProperty("result", out var body)
            || !body.TryGetProperty("data", out var data)
            || data.GetString() is not { Length: > 0 } png)
            throw new InvalidOperationException("Mobile open-agent screenshot did not return an image.");

        return Convert.FromBase64String(png);
    }
}
