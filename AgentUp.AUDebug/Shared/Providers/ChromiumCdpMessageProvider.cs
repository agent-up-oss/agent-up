using System.Text.Json;

namespace AgentUp.AUDebug.Shared.Providers;

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

    public static string CaptureScreenshot(int id = 2, bool captureBeyondViewport = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["format"] = "png",
            ["fromSurface"] = true
        };
        if (captureBeyondViewport)
            parameters["captureBeyondViewport"] = true;

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["method"] = "Page.captureScreenshot",
            ["params"] = parameters
        });
    }

    public static string SetDeviceMetrics(int width, int height, int id)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["method"] = "Emulation.setDeviceMetricsOverride",
            ["params"] = new Dictionary<string, object?>
            {
                ["width"] = width,
                ["height"] = height,
                ["deviceScaleFactor"] = 1,
                ["mobile"] = false
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

    public static (int Width, int Height) ReadSize(string json)
    {
        ThrowIfEvaluateFailed(json, "page size");
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var body)
            || !body.TryGetProperty("result", out var result)
            || !result.TryGetProperty("value", out var value)
            || !value.TryGetProperty("width", out var width)
            || !value.TryGetProperty("height", out var height)
            || width.ValueKind != JsonValueKind.Number
            || height.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException("page size did not return width and height.");

        return (width.GetInt32(), height.GetInt32());
    }

    public static byte[] ReadPng(string json, string action = "Mobile open-agent screenshot")
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException($"{action} failed: {error}");
        if (!document.RootElement.TryGetProperty("result", out var body)
            || !body.TryGetProperty("data", out var data)
            || data.GetString() is not { Length: > 0 } png)
            throw new InvalidOperationException($"{action} did not return an image.");

        return Convert.FromBase64String(png);
    }
}
